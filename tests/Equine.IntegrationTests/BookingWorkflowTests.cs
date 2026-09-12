using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Equine.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Equine.IntegrationTests;

public class BookingWorkflowTests : IClassFixture<EquineApiFactory>
{
    private readonly EquineApiFactory _factory;
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public BookingWorkflowTests(EquineApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Concurrent_bookings_for_same_slot_one_succeeds_one_conflicts()
    {
        var setup = _factory.CreateClient();
        await LoginAsync(setup);
        var (ownerId, horseId, treatmentId, startsAt) = await SeedBookableAsync(setup);

        var a = _factory.CreateClient();
        var b = _factory.CreateClient();
        await LoginAsync(a);
        await LoginAsync(b);

        var body = new
        {
            startsAt,
            lines = new[] { new { horseId, treatmentTypeId = treatmentId } }
        };

        var tasks = new[]
        {
            a.PostAsJsonAsync("/api/app/bookings", body),
            b.PostAsJsonAsync("/api/app/bookings", body)
        };
        var results = await Task.WhenAll(tasks);

        results.Count(r => r.StatusCode == HttpStatusCode.Created).ShouldBe(1);
        var conflict = results.Single(r => r.StatusCode == HttpStatusCode.Conflict);
        var payload = await conflict.Content.ReadFromJsonAsync<JsonElement>(Json);
        payload.GetProperty("slots").ValueKind.ShouldBe(JsonValueKind.Array);
    }

    [Fact]
    public async Task Complete_creates_journal_cancel_frees_slot_and_transitions_are_audited()
    {
        var client = _factory.CreateClient();
        await LoginAsync(client);
        var (ownerId, horseId, treatmentId, startsAt) = await SeedBookableAsync(client, hour: 11);

        var created = await client.PostAsJsonAsync("/api/app/bookings", new
        {
            startsAt,
            lines = new[] { new { horseId, treatmentTypeId = treatmentId, clientNote = "öm i ryggen" } }
        });
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var createdBody = await created.Content.ReadFromJsonAsync<JsonElement>(Json);
        var lineId = createdBody.GetProperty("lineIds")[0].GetGuid();

        var complete = await client.PostAsync($"/api/app/bookings/{lineId}/complete", null);
        complete.StatusCode.ShouldBe(HttpStatusCode.OK);
        var completeBody = await complete.Content.ReadFromJsonAsync<JsonElement>(Json);
        var journalId = completeBody.GetProperty("journalId").GetGuid();

        var journal = await client.GetAsync($"/api/app/journals/{journalId}");
        journal.StatusCode.ShouldBe(HttpStatusCode.OK);
        var journalBody = await journal.Content.ReadFromJsonAsync<JsonElement>(Json);
        journalBody.GetProperty("anamnes").GetString().ShouldBe("öm i ryggen");
        journalBody.GetProperty("status").GetString().ShouldBe("Draft");

        var (owner2, horse2, _, starts2) = await SeedBookableAsync(client, hour: 13);
        var second = await client.PostAsJsonAsync("/api/app/bookings", new
        {
            startsAt = starts2,
            lines = new[] { new { horseId = horse2, treatmentTypeId = treatmentId } }
        });
        second.EnsureSuccessStatusCode();
        var secondId = (await second.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("lineIds")[0].GetGuid();

        var cancel = await client.PostAsJsonAsync($"/api/app/bookings/{secondId}/cancel", new { reason = "ägare sjuk" });
        cancel.StatusCode.ShouldBe(HttpStatusCode.OK);

        var from = DateOnly.FromDateTime(starts2.UtcDateTime);
        var slots = await client.GetAsync($"/api/app/availability/slots?treatmentTypeId={treatmentId}&from={from:yyyy-MM-dd}&to={from:yyyy-MM-dd}&applyMinNotice=false");
        slots.EnsureSuccessStatusCode();
        var slotList = await slots.Content.ReadFromJsonAsync<JsonElement>(Json);
        slotList.GetArrayLength().ShouldBeGreaterThan(0);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EquineDbContext>();
        var actions = await db.AuditLog.Select(a => a.Action).ToListAsync();
        actions.ShouldContain("BOOKING_CREATE");
        actions.ShouldContain("BOOKING_COMPLETE");
        actions.ShouldContain("BOOKING_CANCEL");
        actions.ShouldContain("JOURNAL_CREATE");
    }

    [Fact]
    public async Task Booking_succeeds_when_horse_has_empty_stable_and_owner_has_address()
    {
        var client = _factory.CreateClient();
        await LoginAsync(client);

        var owner = await client.PostAsJsonAsync("/api/app/owners", new
        {
            name = "Ola Olsson",
            email = $"ola-{Guid.NewGuid():N}@ex.se",
            phone = "0701234567",
            addressStreet = "Storgatan 1",
            addressPostcode = "27531",
            addressCity = "Sjöbo"
        });
        owner.EnsureSuccessStatusCode();
        var ownerId = (await owner.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetGuid();

        var horse = await client.PostAsJsonAsync("/api/app/horses", new
        {
            ownerId,
            name = $"Häst-{Guid.NewGuid():N}"[..12],
            species = "Hast",
            sex = "Sto",
            birthYear = 2018,
            stableAddress = "",
            stablePostcode = "",
            stableCity = ""
        });
        horse.EnsureSuccessStatusCode();
        var horseId = (await horse.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetGuid();

        var (_, _, treatmentId, startsAt) = await SeedBookableAsync(client, hour: 15);

        var created = await client.PostAsJsonAsync("/api/app/bookings", new
        {
            startsAt,
            lines = new[] { new { horseId, treatmentTypeId = treatmentId } }
        });
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Changing_availability_rule_invalidates_slot_cache()
    {
        var client = _factory.CreateClient();
        await LoginAsync(client);
        var (_, _, treatmentId, startsAt) = await SeedBookableAsync(client, hour: 10);
        var stockholm = TimeZoneInfo.FindSystemTimeZoneById("Europe/Stockholm");
        var local = TimeZoneInfo.ConvertTime(startsAt, stockholm);
        var day = DateOnly.FromDateTime(local.DateTime);
        var me = await client.GetFromJsonAsync<JsonElement>("/api/app/me", Json);
        var practitionerId = me.GetProperty("id").GetGuid();
        var slotsUrl = $"/api/app/availability/slots?practitionerId={practitionerId}&treatmentTypeId={treatmentId}&from={day:yyyy-MM-dd}&to={day:yyyy-MM-dd}&applyMinNotice=false";

        var before = await client.GetAsync(slotsUrl);
        if (!before.IsSuccessStatusCode)
            throw new HttpRequestException($"Slots failed {(int)before.StatusCode}: {await before.Content.ReadAsStringAsync()}");
        var beforeCount = (await before.Content.ReadFromJsonAsync<JsonElement>(Json)).GetArrayLength();
        beforeCount.ShouldBeGreaterThan(0);

        var dayStart = TimeZoneInfo.ConvertTimeToUtc(new DateTime(local.Year, local.Month, local.Day, 0, 0, 0, DateTimeKind.Unspecified), stockholm);
        var timeOff = await client.PostAsJsonAsync("/api/app/time-off", new
        {
            startsAt = new DateTimeOffset(dayStart, TimeSpan.Zero),
            endsAt = new DateTimeOffset(dayStart.AddDays(1), TimeSpan.Zero),
            allDay = true,
            reason = "stängt"
        });
        if (!timeOff.IsSuccessStatusCode)
            throw new HttpRequestException($"Time-off failed {(int)timeOff.StatusCode}: {await timeOff.Content.ReadAsStringAsync()}");

        var after = await client.GetAsync(slotsUrl);
        after.EnsureSuccessStatusCode();
        var afterCount = (await after.Content.ReadFromJsonAsync<JsonElement>(Json)).GetArrayLength();
        afterCount.ShouldBe(0);
    }

    private static async Task LoginAsync(HttpClient client)
    {
        var login = await client.PostAsJsonAsync("/api/app/auth/login", new
        {
            email = "victor@gradera.nu",
            password = "Admin@123456"
        });
        login.EnsureSuccessStatusCode();
        var payload = await login.Content.ReadFromJsonAsync<JsonElement>(Json);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", payload.GetProperty("accessToken").GetString());
    }

    private static async Task<(Guid OwnerId, Guid HorseId, Guid TreatmentId, DateTimeOffset StartsAt)> SeedBookableAsync(
        HttpClient client,
        int hour = 10)
    {
        var owner = await client.PostAsJsonAsync("/api/app/owners", new
        {
            name = "Anna Ägare",
            email = $"anna-{Guid.NewGuid():N}@ex.se",
            phone = "0701234567",
            addressStreet = "Stallet 1",
            addressPostcode = "27531",
            addressCity = "Sjöbo"
        });
        owner.EnsureSuccessStatusCode();
        var ownerId = (await owner.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetGuid();

        var horse = await client.PostAsJsonAsync("/api/app/horses", new
        {
            ownerId,
            name = $"Häst-{Guid.NewGuid():N}"[..12],
            species = "Hast",
            sex = "Sto",
            birthYear = 2018,
            stableAddress = "Stallet 1",
            stablePostcode = "27531",
            stableCity = "Sjöbo"
        });
        horse.EnsureSuccessStatusCode();
        var horseId = (await horse.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetGuid();

        var treatment = await client.PostAsJsonAsync("/api/app/treatment-types", new
        {
            name = $"Massage-{Guid.NewGuid():N}"[..16],
            shortDescription = "Kropp",
            durationMinutes = 30,
            bufferBeforeMinutes = 0,
            bufferAfterMinutes = 0,
            colour = "#3d6b1e"
        });
        treatment.EnsureSuccessStatusCode();
        var treatmentId = (await treatment.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetGuid();

        var stockholm = TimeZoneInfo.FindSystemTimeZoneById("Europe/Stockholm");
        var local = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(14).AddHours(hour), DateTimeKind.Unspecified);
        while (local.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            local = local.AddDays(1);
        var startsAt = TimeZoneInfo.ConvertTimeToUtc(local, stockholm);

        var existing = await client.GetAsync("/api/app/availability-rules");
        existing.EnsureSuccessStatusCode();
        var rules = await existing.Content.ReadFromJsonAsync<JsonElement>(Json);
        if (rules.GetArrayLength() == 0)
        {
            foreach (var day in new[] { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday" })
            {
                var rule = await client.PostAsJsonAsync("/api/app/availability-rules", new
                {
                    dayOfWeek = day,
                    startTime = "08:00:00",
                    endTime = "17:00:00",
                    active = true
                });
                rule.EnsureSuccessStatusCode();
            }
        }

        return (ownerId, horseId, treatmentId, new DateTimeOffset(startsAt, TimeSpan.Zero));
    }
}
