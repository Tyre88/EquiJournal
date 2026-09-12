using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;
using Xunit;

namespace Equine.IntegrationTests;

public class ReportTests : IClassFixture<EquineApiFactory>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public ReportTests(EquineApiFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task Treatments_report_returns_empty_array_when_no_matching_journals()
    {
        await LoginAsync();
        var from = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-20));
        var to = from.AddDays(1);

        var res = await _client.GetAsync($"/api/app/reports/behandlingar?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}");
        res.StatusCode.ShouldBe(HttpStatusCode.OK);
        var rows = await res.Content.ReadFromJsonAsync<JsonElement>(Json);
        rows.ValueKind.ShouldBe(JsonValueKind.Array);
        rows.GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task Treatments_report_aggregates_signed_journals_by_month()
    {
        await LoginAsync();
        var ownerId = await CreateOwnerAsync("Rapport Ägare");
        var horseId = await CreateHorseAsync(ownerId, "RapportHäst");
        var treatmentName = $"RapportBeh-{Guid.NewGuid():N}"[..16];
        var treatmentId = await CreateTreatmentAsync(treatmentName, 850);

        await CreateAndSignJournalAsync(horseId, treatmentId, 850);
        await CreateAndSignJournalAsync(horseId, treatmentId, 850);

        var from = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
        var to = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        var res = await _client.GetAsync($"/api/app/reports/behandlingar?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}");
        res.StatusCode.ShouldBe(HttpStatusCode.OK);
        var rows = await res.Content.ReadFromJsonAsync<JsonElement>(Json);
        var match = Enumerable.Range(0, rows.GetArrayLength())
            .Select(i => rows[i])
            .Single(r => r.GetProperty("treatmentName").GetString() == treatmentName);
        match.GetProperty("count").GetInt32().ShouldBe(2);
        match.GetProperty("revenueEstimate").GetDecimal().ShouldBe(1700);
    }

    [Fact]
    public async Task Revenue_and_booking_source_reports_return_ok()
    {
        await LoginAsync();
        var from = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-3));
        var to = DateOnly.FromDateTime(DateTime.UtcNow);
        var revenue = await _client.GetAsync($"/api/app/reports/intakter?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}");
        revenue.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await revenue.Content.ReadFromJsonAsync<JsonElement>(Json)).ValueKind.ShouldBe(JsonValueKind.Array);

        var sources = await _client.GetAsync($"/api/app/reports/bokningskallor?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}");
        sources.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await sources.Content.ReadFromJsonAsync<JsonElement>(Json)).ValueKind.ShouldBe(JsonValueKind.Array);
    }

    private async Task LoginAsync()
    {
        var login = await _client.PostAsJsonAsync("/api/app/auth/login", new
        {
            email = "victor@gradera.nu",
            password = "Admin@123456"
        });
        login.EnsureSuccessStatusCode();
        var payload = await login.Content.ReadFromJsonAsync<JsonElement>(Json);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", payload.GetProperty("accessToken").GetString());
    }

    private async Task<Guid> CreateOwnerAsync(string name)
    {
        var res = await _client.PostAsJsonAsync("/api/app/owners", new
        {
            name,
            email = $"owner-{Guid.NewGuid():N}@ex.se",
            phone = "0700000099"
        });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetGuid();
    }

    private async Task<Guid> CreateHorseAsync(Guid ownerId, string name)
    {
        var res = await _client.PostAsJsonAsync("/api/app/horses", new
        {
            ownerId,
            name,
            species = "Hast",
            sex = "Sto",
            birthYear = 2018,
            identity = $"SE{Guid.NewGuid():N}"[..10]
        });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetGuid();
    }

    private async Task<Guid> CreateTreatmentAsync(string name, decimal price)
    {
        var res = await _client.PostAsJsonAsync("/api/app/treatment-types", new
        {
            name,
            shortDescription = "Kropp",
            durationMinutes = 30,
            bufferBeforeMinutes = 0,
            bufferAfterMinutes = 0,
            colour = "#3d6b1e",
            priceExclVat = price
        });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetGuid();
    }

    private async Task CreateAndSignJournalAsync(Guid horseId, Guid treatmentTypeId, decimal price)
    {
        var create = await _client.PostAsJsonAsync("/api/app/journals", new
        {
            horseId,
            performedAt = DateTimeOffset.UtcNow,
            anamnes = "test",
            statusKlinisk = "ok",
            atgarder = "behandling",
            treatmentTypeId,
            price
        });
        create.EnsureSuccessStatusCode();
        var id = (await create.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetGuid();
        var sign = await _client.PostAsync($"/api/app/journals/{id}/sign", null);
        sign.EnsureSuccessStatusCode();
    }
}
