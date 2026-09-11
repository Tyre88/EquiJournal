using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Shouldly;
using Xunit;

namespace Equine.IntegrationTests;

public class DriveLogTests : IClassFixture<EquineApiFactory>
{
    private readonly EquineApiFactory _factory;
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public DriveLogTests(EquineApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Practice_vehicle_registration_round_trips()
    {
        var client = _factory.CreateClient();
        await LoginAsync(client);
        var put = await client.PutAsJsonAsync("/api/app/practice-settings", new
        {
            name = "Victor",
            clinic = "HästJournal",
            addressStreet = "Storgatan 1",
            addressPostcode = "27531",
            addressCity = "Sjöbo",
            latitude = 55.632,
            longitude = 13.707,
            phone = "0700000000",
            email = "victor@gradera.nu",
            vehicleRegistrationNumber = "abc 123"
        });
        put.EnsureSuccessStatusCode();
        var saved = await put.Content.ReadFromJsonAsync<JsonElement>(Json);
        saved.GetProperty("vehicleRegistrationNumber").GetString().ShouldBe("ABC 123");

        var get = await client.GetFromJsonAsync<JsonElement>("/api/app/practice-settings", Json);
        get.GetProperty("vehicleRegistrationNumber").GetString().ShouldBe("ABC 123");
    }

    [Fact]
    public async Task Drive_log_json_csv_and_pdf_from_bookings()
    {
        var client = _factory.CreateClient();
        await LoginAsync(client);

        (await client.PutAsJsonAsync("/api/app/practice-settings", new
        {
            name = "Victor",
            clinic = "HästJournal",
            addressStreet = "Storgatan 1",
            addressPostcode = "22222",
            addressCity = "Lund",
            latitude = 55.705,
            longitude = 13.193,
            phone = "0700000000",
            email = "victor@gradera.nu",
            vehicleRegistrationNumber = "XYZ 789"
        })).EnsureSuccessStatusCode();

        await EnsureWeekdayRulesAsync(client);
        var treatmentId = await CreateTreatmentAsync(client);
        var day = NextWeekday(21);
        var horseA = await CreateHorseAtAsync(client, "Stall Sjöbo", "Stallvägen 1", "27531", "Sjöbo", 55.632m, 13.707m);
        var horseB = await CreateHorseAtAsync(client, "Stall Ystad", "Hamngatan 2", "27139", "Ystad", 55.429m, 13.820m);

        (await client.PostAsJsonAsync("/api/app/bookings", new
        {
            startsAt = At(day, 9),
            lines = new[] { new { horseId = horseA, treatmentTypeId = treatmentId } }
        })).EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync("/api/app/bookings", new
        {
            startsAt = At(day, 11),
            lines = new[] { new { horseId = horseB, treatmentTypeId = treatmentId } }
        })).EnsureSuccessStatusCode();

        var from = day.AddDays(-1).ToString("yyyy-MM-dd");
        var to = day.AddDays(1).ToString("yyyy-MM-dd");

        var jsonRes = await client.GetAsync($"/api/app/reports/korjournal?from={from}&to={to}");
        jsonRes.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await jsonRes.Content.ReadFromJsonAsync<JsonElement>(Json);
        body.GetProperty("meta").GetProperty("vehicleRegistrationNumber").GetString().ShouldBe("XYZ 789");
        body.GetProperty("meta").GetProperty("tripCount").GetInt32().ShouldBeGreaterThanOrEqualTo(3);
        var trips = body.GetProperty("trips");
        trips.GetArrayLength().ShouldBeGreaterThanOrEqualTo(3);
        var purposes = Enumerable.Range(0, trips.GetArrayLength())
            .Select(i => trips[i].GetProperty("purpose").GetString())
            .ToList();
        purposes.ShouldContain("Tjänsteresa");
        purposes.ShouldContain("Hemresa");

        var csvRes = await client.GetAsync($"/api/app/reports/korjournal?from={from}&to={to}&format=csv");
        csvRes.StatusCode.ShouldBe(HttpStatusCode.OK);
        (csvRes.Content.Headers.ContentType?.MediaType ?? "").ShouldContain("csv");
        var csv = Encoding.UTF8.GetString(await csvRes.Content.ReadAsByteArrayAsync());
        csv.ShouldContain("Registreringsnummer");
        csv.ShouldContain("Tjänsteresa");
        csv.ShouldContain("XYZ 789");

        var pdfRes = await client.GetAsync($"/api/app/reports/korjournal?from={from}&to={to}&format=pdf");
        pdfRes.StatusCode.ShouldBe(HttpStatusCode.OK);
        pdfRes.Content.Headers.ContentType?.MediaType.ShouldBe("application/pdf");
        var pdf = await pdfRes.Content.ReadAsByteArrayAsync();
        pdf.Length.ShouldBeGreaterThan(100);
        Encoding.ASCII.GetString(pdf[..4]).ShouldBe("%PDF");
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

    private static async Task EnsureWeekdayRulesAsync(HttpClient client)
    {
        var existing = await client.GetAsync("/api/app/availability-rules");
        existing.EnsureSuccessStatusCode();
        var rules = await existing.Content.ReadFromJsonAsync<JsonElement>(Json);
        if (rules.GetArrayLength() > 0) return;

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

    private static async Task<Guid> CreateTreatmentAsync(HttpClient client)
    {
        var treatment = await client.PostAsJsonAsync("/api/app/treatment-types", new
        {
            name = $"Körlogg-{Guid.NewGuid():N}"[..16],
            shortDescription = "Kropp",
            durationMinutes = 30,
            bufferBeforeMinutes = 0,
            bufferAfterMinutes = 0,
            colour = "#3d6b1e"
        });
        treatment.EnsureSuccessStatusCode();
        return (await treatment.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateHorseAtAsync(
        HttpClient client,
        string ownerName,
        string street,
        string postcode,
        string city,
        decimal lat,
        decimal lon)
    {
        var owner = await client.PostAsJsonAsync("/api/app/owners", new
        {
            name = ownerName,
            email = $"{Guid.NewGuid():N}@ex.se",
            phone = "0701234567",
            addressStreet = street,
            addressPostcode = postcode,
            addressCity = city,
            latitude = lat,
            longitude = lon
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
            stableAddress = street,
            stablePostcode = postcode,
            stableCity = city,
            stableLatitude = lat,
            stableLongitude = lon
        });
        horse.EnsureSuccessStatusCode();
        return (await horse.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetGuid();
    }

    private static DateOnly NextWeekday(int daysAhead)
    {
        var stockholm = TimeZoneInfo.FindSystemTimeZoneById("Europe/Stockholm");
        var local = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, stockholm).Date.AddDays(daysAhead);
        while (local.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            local = local.AddDays(1);
        return DateOnly.FromDateTime(local);
    }

    private static DateTimeOffset At(DateOnly day, int hour)
    {
        var stockholm = TimeZoneInfo.FindSystemTimeZoneById("Europe/Stockholm");
        var local = DateTime.SpecifyKind(day.ToDateTime(new TimeOnly(hour, 0)), DateTimeKind.Unspecified);
        var utc = TimeZoneInfo.ConvertTimeToUtc(local, stockholm);
        return new DateTimeOffset(utc, TimeSpan.Zero);
    }
}
