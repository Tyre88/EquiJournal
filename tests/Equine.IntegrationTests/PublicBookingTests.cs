using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Equine.Infrastructure;
using Equine.Infrastructure.Email;
using Equine.Infrastructure.Notifications;
using Equine.Infrastructure.Tokens;
using Equine.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Equine.IntegrationTests;

public class PublicBookingTests : IClassFixture<EquineApiFactory>
{
    private readonly EquineApiFactory _factory;
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public PublicBookingTests(EquineApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Public_treatments_and_slots_never_leak_personal_data()
    {
        var admin = _factory.CreateClient();
        await LoginAsync(admin);
        var (_, _, treatmentId, startsAt) = await SeedOnlineAsync(admin, hour: 8, dayOffset: 20, ownerName: "Hemlig Ägare", horseName: "HemligHäst");

        var client = _factory.CreateClient();
        var treatments = await client.GetFromJsonAsync<JsonElement>("/api/public/default/treatments", Json);
        var body = treatments.GetRawText();
        body.ShouldNotContain("Hemlig");
        body.ShouldNotContain("owner", Case.Insensitive);
        body.ShouldNotContain("horseName", Case.Insensitive);

        var from = DateOnly.FromDateTime(startsAt.UtcDateTime.AddDays(-1));
        var to = from.AddDays(10);
        var slots = await client.GetAsync($"/api/public/default/slots?treatmentId={treatmentId}&from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}&postcode=27531");
        slots.EnsureSuccessStatusCode();
        var slotBody = await slots.Content.ReadAsStringAsync();
        slotBody.ShouldNotContain("Hemlig");
        slotBody.ShouldNotContain("unavailable", Case.Insensitive);
        slotBody.ShouldNotContain("timeOff", Case.Insensitive);
        var parsed = JsonDocument.Parse(slotBody).RootElement;
        parsed.TryGetProperty("starts", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Slot_lists_are_indistinguishable_for_different_block_reasons()
    {
        var admin = _factory.CreateClient();
        await LoginAsync(admin);
        var (_, _, treatmentId, startsAt) = await SeedOnlineAsync(admin, hour: 9, dayOffset: 22);

        var publicClient = _factory.CreateClient();
        var stockholm = TimeZoneInfo.FindSystemTimeZoneById("Europe/Stockholm");
        var local = TimeZoneInfo.ConvertTime(startsAt, stockholm);
        var day = DateOnly.FromDateTime(local.DateTime);
        var url = $"/api/public/default/slots?treatmentId={treatmentId}&from={day:yyyy-MM-dd}&to={day:yyyy-MM-dd}&postcode=27531";

        var before = await publicClient.GetFromJsonAsync<JsonElement>(url, Json);
        var beforeCount = before.GetProperty("starts").GetArrayLength();
        beforeCount.ShouldBeGreaterThan(0);

        await BookManualAsync(admin, startsAt, treatmentId);
        var afterBooking = await publicClient.GetFromJsonAsync<JsonElement>(url, Json);

        var (_, _, treatment2, starts2) = await SeedOnlineAsync(admin, hour: 10, dayOffset: 21);
        var local2 = TimeZoneInfo.ConvertTime(starts2, stockholm);
        var day2 = DateOnly.FromDateTime(local2.DateTime);
        var timeOff = await admin.PostAsJsonAsync("/api/app/time-off", new
        {
            startsAt = starts2,
            endsAt = starts2.AddHours(2),
            allDay = false,
            reason = "semester"
        });
        timeOff.EnsureSuccessStatusCode();
        var afterTimeOff = await publicClient.GetFromJsonAsync<JsonElement>(
            $"/api/public/default/slots?treatmentId={treatment2}&from={day2:yyyy-MM-dd}&to={day2:yyyy-MM-dd}&postcode=27531", Json);

        afterBooking.GetProperty("starts").ValueKind.ShouldBe(JsonValueKind.Array);
        afterTimeOff.GetProperty("starts").ValueKind.ShouldBe(JsonValueKind.Array);
        afterBooking.TryGetProperty("reason", out _).ShouldBeFalse();
        afterTimeOff.TryGetProperty("reason", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task Concurrent_public_bookings_one_succeeds_one_conflicts()
    {
        var admin = _factory.CreateClient();
        await LoginAsync(admin);
        var (_, _, treatmentId, startsAt) = await SeedOnlineAsync(admin, hour: 11, dayOffset: 24);

        var a = _factory.CreateClient();
        var b = _factory.CreateClient();
        var bodyA = PublicBody(treatmentId, startsAt, "anna-a@ex.se");
        var bodyB = PublicBody(treatmentId, startsAt, "anna-b@ex.se");
        var results = await Task.WhenAll(
            a.PostAsJsonAsync("/api/public/default/bookings", bodyA),
            b.PostAsJsonAsync("/api/public/default/bookings", bodyB));

        results.Count(r => r.StatusCode == HttpStatusCode.Created).ShouldBe(1);
        var conflict = results.Single(r => r.StatusCode == HttpStatusCode.Conflict);
        var payload = await conflict.Content.ReadFromJsonAsync<JsonElement>(Json);
        payload.GetProperty("slots").ValueKind.ShouldBe(JsonValueKind.Array);
        payload.GetProperty("message").GetString().ShouldNotBeNull();
    }

    [Fact]
    public async Task Unknown_and_expired_tokens_are_identical()
    {
        var client = _factory.CreateClient();
        var unknown = await client.GetAsync("/api/public/bookings/not-a-real-token");
        unknown.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var unknownBody = await unknown.Content.ReadAsStringAsync();

        using var scope = _factory.Services.CreateScope();
        var tokens = scope.ServiceProvider.GetRequiredService<PublicBookingTokenService>();
        var expired = tokens.Protect(Guid.CreateVersion7(), DateTimeOffset.UtcNow.AddMinutes(-5), PublicBookingTokenService.ManagePurpose);
        var expiredRes = await client.GetAsync($"/api/public/bookings/{expired}");
        expiredRes.StatusCode.ShouldBe(unknown.StatusCode);
        (await expiredRes.Content.ReadAsStringAsync()).ShouldBe(unknownBody);
    }

    [Fact]
    public async Task Email_match_is_silent_and_verify_confirms()
    {
        var admin = _factory.CreateClient();
        await LoginAsync(admin);
        var email = $"sam-{Guid.NewGuid():N}@ex.se";
        var (ownerId, _, treatmentId, startsAt) = await SeedOnlineAsync(admin, hour: 12, dayOffset: 26, email: email);

        var publicClient = _factory.CreateClient();
        var created = await publicClient.PostAsJsonAsync("/api/public/default/bookings", PublicBody(treatmentId, startsAt, email, horseName: "SammeHäst"));
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var createdBody = await created.Content.ReadAsStringAsync();
        createdBody.ShouldNotContain(ownerId.ToString());
        createdBody.ShouldNotContain("match", Case.Insensitive);

        var token = await ReadVerifyTokenAsync();
        var verify = await publicClient.PostAsJsonAsync("/api/public/bookings/verify", new { token });
        verify.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var scope = await _factory.CreateTenantScopeAsync();
        var db = scope.ServiceProvider.GetRequiredService<EquineDbContext>();
        var owners = await db.Owners.Where(o => o.Email == email).ToListAsync();
        owners.Count.ShouldBe(1);
        var line = await db.BookingLines.OrderByDescending(l => l.CreatedAt).FirstAsync(l => l.OwnerId == owners[0].Id);
        line.EmailVerifiedAt.ShouldNotBeNull();
        line.Status.ShouldBe(Equine.Domain.Entities.BookingStatus.Confirmed);
    }

    [Fact]
    public async Task Expire_job_releases_slot()
    {
        var admin = _factory.CreateClient();
        await LoginAsync(admin);
        var (_, _, treatmentId, startsAt) = await SeedOnlineAsync(admin, hour: 13, dayOffset: 28);

        var publicClient = _factory.CreateClient();
        var created = await publicClient.PostAsJsonAsync("/api/public/default/bookings", PublicBody(treatmentId, startsAt, $"exp-{Guid.NewGuid():N}@ex.se"));
        created.StatusCode.ShouldBe(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EquineDbContext>();
            await db.Database.ExecuteSqlRawAsync("""UPDATE booking_lines SET "CreatedAt" = now() - interval '2 hours' WHERE "Source" = 'Widget' AND "EmailVerifiedAt" IS NULL""");
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var job = scope.ServiceProvider.GetRequiredService<ExpireUnverifiedBookingsService>();
            (await job.RunAsync()).ShouldBeGreaterThan(0);
        }

        var again = await publicClient.PostAsJsonAsync("/api/public/default/bookings", PublicBody(treatmentId, startsAt, $"exp2-{Guid.NewGuid():N}@ex.se"));
        again.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Honeypot_and_too_fast_do_not_occupy_slot()
    {
        var admin = _factory.CreateClient();
        await LoginAsync(admin);
        var (_, _, treatmentId, startsAt) = await SeedOnlineAsync(admin, hour: 14, dayOffset: 30);

        var publicClient = _factory.CreateClient();
        var honey = PublicBody(treatmentId, startsAt, $"bot-{Guid.NewGuid():N}@ex.se") with { Website = "http://spam" };
        var honeyRes = await publicClient.PostAsJsonAsync("/api/public/default/bookings", honey);
        honeyRes.StatusCode.ShouldBe(HttpStatusCode.Created);

        var fast = PublicBody(treatmentId, startsAt, $"fast-{Guid.NewGuid():N}@ex.se") with { FormOpenedAt = DateTimeOffset.UtcNow };
        var fastRes = await publicClient.PostAsJsonAsync("/api/public/default/bookings", fast);
        fastRes.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var real = await publicClient.PostAsJsonAsync("/api/public/default/bookings", PublicBody(treatmentId, startsAt, $"real-{Guid.NewGuid():N}@ex.se"));
        real.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Manage_token_cannot_read_another_booking()
    {
        var admin = _factory.CreateClient();
        await LoginAsync(admin);
        var (_, _, treatmentId, startsAt) = await SeedOnlineAsync(admin, hour: 8, dayOffset: 32);
        var (_, _, _, starts2) = await SeedOnlineAsync(admin, hour: 15, dayOffset: 32, reuseTreatment: treatmentId);

        var publicClient = _factory.CreateClient();
        (await publicClient.PostAsJsonAsync("/api/public/default/bookings", PublicBody(treatmentId, startsAt, $"m1-{Guid.NewGuid():N}@ex.se"))).EnsureSuccessStatusCode();
        var token1 = await ReadVerifyTokenAsync();
        (await publicClient.PostAsJsonAsync("/api/public/bookings/verify", new { token = token1 })).EnsureSuccessStatusCode();

        (await publicClient.PostAsJsonAsync("/api/public/default/bookings", PublicBody(treatmentId, starts2, $"m2-{Guid.NewGuid():N}@ex.se"))).EnsureSuccessStatusCode();
        var token2 = await ReadVerifyTokenAsync();
        (await publicClient.PostAsJsonAsync("/api/public/bookings/verify", new { token = token2 })).EnsureSuccessStatusCode();

        using var scope = await _factory.CreateTenantScopeAsync();
        var tokens = scope.ServiceProvider.GetRequiredService<PublicBookingTokenService>();
        var db = scope.ServiceProvider.GetRequiredService<EquineDbContext>();
        var lines = await db.BookingLines.Where(l => l.Source == Equine.Domain.Entities.BookingSource.Widget)
            .OrderBy(l => l.CreatedAt).ToListAsync();
        lines.Count.ShouldBeGreaterThanOrEqualTo(2);
        var manage2 = tokens.Protect(lines[^1].Id, DateTimeOffset.UtcNow.AddDays(7), PublicBookingTokenService.ManagePurpose);
        var summary = await publicClient.GetFromJsonAsync<JsonElement>($"/api/public/bookings/{manage2}", Json);
        summary.GetProperty("publicReference").GetString().ShouldBe(lines[^1].PublicReference);
        summary.TryGetProperty("ownerName", out _).ShouldBeFalse();
        summary.TryGetProperty("horseName", out _).ShouldBeFalse();
    }

    private async Task<string> ReadVerifyTokenAsync()
    {
        var sender = _factory.Services.GetRequiredService<RecordingEmailSender>();
        var mail = sender.Sent.LastOrDefault(m => m.Subject.Contains("Bekräfta", StringComparison.OrdinalIgnoreCase));
        if (mail is null)
        {
            using var scope = await _factory.CreateTenantScopeAsync();
            await scope.ServiceProvider.GetRequiredService<NotificationDispatcher>().DispatchDueAsync();
            mail = sender.Sent.LastOrDefault(m => m.Subject.Contains("Bekräfta", StringComparison.OrdinalIgnoreCase));
        }
        mail.ShouldNotBeNull(
            $"subjects: {string.Join(" | ", sender.Sent.Select(m => m.Subject))}");
        var marker = "token=";
        var idx = mail!.TextBody.IndexOf(marker, StringComparison.Ordinal);
        idx.ShouldBeGreaterThan(-1);
        var start = idx + marker.Length;
        var end = mail.TextBody.IndexOfAny([' ', '\r', '\n'], start);
        if (end < 0) end = mail.TextBody.Length;
        return Uri.UnescapeDataString(mail.TextBody[start..end].Trim());
    }

    private static PublicCreate PublicBody(
        Guid treatmentId,
        DateTimeOffset startsAt,
        string email,
        string horseName = "TestHäst") => new(
        treatmentId,
        startsAt,
        new PublicHorse(horseName, 2018, null, "Valack", null),
        new PublicOwner("Anna Test", email, "0701234567", "Stallet 1", "27531", "Sjöbo"),
        true,
        "öm i ryggen",
        null,
        DateTimeOffset.UtcNow.AddSeconds(-15));

    private async Task BookManualAsync(HttpClient admin, DateTimeOffset startsAt, Guid treatmentId)
    {
        var owner = await admin.PostAsJsonAsync("/api/app/owners", new
        {
            name = "Manuell",
            email = $"man-{Guid.NewGuid():N}@ex.se",
            phone = "0701111111",
            addressStreet = "Stallet 1",
            addressPostcode = "27531",
            addressCity = "Sjöbo"
        });
        var ownerId = (await owner.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetGuid();
        var horse = await admin.PostAsJsonAsync("/api/app/horses", new
        {
            ownerId,
            name = $"M-{Guid.NewGuid():N}"[..10],
            species = "Hast",
            sex = "Sto",
            birthYear = 2017,
            stableAddress = "Stallet 1",
            stablePostcode = "27531",
            stableCity = "Sjöbo"
        });
        var horseId = (await horse.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetGuid();
        var created = await admin.PostAsJsonAsync("/api/app/bookings", new
        {
            startsAt,
            lines = new[] { new { horseId, treatmentTypeId = treatmentId } }
        });
        created.EnsureSuccessStatusCode();
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

    private static async Task<(Guid OwnerId, Guid HorseId, Guid TreatmentId, DateTimeOffset StartsAt)> SeedOnlineAsync(
        HttpClient client,
        int hour = 10,
        int dayOffset = 14,
        string? email = null,
        string ownerName = "Anna Ägare",
        string horseName = "Häst",
        Guid? reuseTreatment = null)
    {
        var owner = await client.PostAsJsonAsync("/api/app/owners", new
        {
            name = ownerName,
            email = email ?? $"anna-{Guid.NewGuid():N}@ex.se",
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
            name = $"{horseName}-{Guid.NewGuid():N}"[..12],
            species = "Hast",
            sex = "Sto",
            birthYear = 2018,
            stableAddress = "Stallet 1",
            stablePostcode = "27531",
            stableCity = "Sjöbo"
        });
        horse.EnsureSuccessStatusCode();
        var horseId = (await horse.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetGuid();

        Guid treatmentId;
        if (reuseTreatment is Guid existing)
        {
            treatmentId = existing;
        }
        else
        {
            var treatment = await client.PostAsJsonAsync("/api/app/treatment-types", new
            {
                name = $"Massage-{Guid.NewGuid():N}"[..16],
                shortDescription = "Kropp",
                durationMinutes = 30,
                bookableOnline = true,
                requiresApproval = false,
                minNoticeHours = 0,
                maxAdvanceDays = 90
            });
            treatment.EnsureSuccessStatusCode();
            treatmentId = (await treatment.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetGuid();
        }

        var stockholm = TimeZoneInfo.FindSystemTimeZoneById("Europe/Stockholm");
        var local = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(dayOffset).AddHours(hour), DateTimeKind.Unspecified);
        while (local.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            local = local.AddDays(1);
        var startsAt = TimeZoneInfo.ConvertTimeToUtc(local, stockholm);

        var existingRules = await client.GetAsync("/api/app/availability-rules");
        existingRules.EnsureSuccessStatusCode();
        var rules = await existingRules.Content.ReadFromJsonAsync<JsonElement>(Json);
        if (rules.GetArrayLength() == 0)
        {
            foreach (var day in new[] { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday" })
            {
                (await client.PostAsJsonAsync("/api/app/availability-rules", new
                {
                    dayOfWeek = day,
                    startTime = "08:00:00",
                    endTime = "17:00:00",
                    active = true
                })).EnsureSuccessStatusCode();
            }
        }

        return (ownerId, horseId, treatmentId, new DateTimeOffset(startsAt, TimeSpan.Zero));
    }

    private sealed record PublicCreate(
        Guid TreatmentId,
        DateTimeOffset StartsAt,
        PublicHorse Horse,
        PublicOwner Owner,
        bool Consent,
        string? Note,
        string? Website,
        DateTimeOffset? FormOpenedAt);

    private sealed record PublicHorse(string Name, int? BirthYear, string? AgeGroup, string? Breed, string? KnownIssues);
    private sealed record PublicOwner(string Name, string Email, string Phone, string AddressStreet, string AddressPostcode, string? AddressCity);
}

public class PublicRateLimitTests : IClassFixture<PublicRateLimitFactory>
{
    private readonly PublicRateLimitFactory _factory;
    public PublicRateLimitTests(PublicRateLimitFactory factory) => _factory = factory;

    [Fact]
    public async Task Public_booking_rate_limit_returns_429()
    {
        var client = _factory.CreateClient();
        HttpResponseMessage? last = null;
        for (var i = 0; i < 4; i++)
        {
            last = await client.PostAsJsonAsync("/api/public/default/bookings", new
            {
                treatmentId = Guid.CreateVersion7(),
                startsAt = DateTimeOffset.UtcNow.AddDays(3),
                horse = new { name = "X", birthYear = 2010 },
                owner = new { name = "Y", email = $"rl-{i}@ex.se", phone = "070", addressStreet = "A", addressPostcode = "27531" },
                consent = true,
                formOpenedAt = DateTimeOffset.UtcNow.AddSeconds(-20)
            });
        }
        last!.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }
}

public class PublicRateLimitFactory : EquineApiFactory
{
    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimiting:PublicBookingPermitLimit"] = "2",
                ["RateLimiting:PublicBookingWindowMinutes"] = "60",
                ["RateLimiting:PublicBookingEmailPermitLimit"] = "100"
            });
        });
    }
}
