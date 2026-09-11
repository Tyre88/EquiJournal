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

public class M5PolishTests : IClassFixture<EquineApiFactory>
{
    private readonly EquineApiFactory _factory;
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public M5PolishTests(EquineApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Global_search_returns_grouped_results()
    {
        await LoginAsync();
        var ownerId = await CreateOwnerAsync("Searchtest Owner");
        await CreateHorseAsync(ownerId, "Blixten");

        var res = await _client.GetAsync("/api/app/search?q=Blixten");
        res.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>(Json);
        var total = body.GetProperty("horses").GetArrayLength()
                    + body.GetProperty("clients").GetArrayLength()
                    + body.GetProperty("journals").GetArrayLength()
                    + body.GetProperty("bookings").GetArrayLength();
        total.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task Reports_endpoint_returns_json()
    {
        await LoginAsync();
        var res = await _client.GetAsync("/api/app/reports/osignerade");
        res.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Reports_csv_export_works()
    {
        await LoginAsync();
        var res = await _client.GetAsync("/api/app/reports/osignerade?format=csv");
        res.StatusCode.ShouldBe(HttpStatusCode.OK);
        res.Content.Headers.ContentType?.MediaType.ShouldContain("csv");
        var bytes = await res.Content.ReadAsByteArrayAsync();
        bytes.Length.ShouldBeGreaterThan(3);
    }

    [Fact]
    public async Task Audit_list_and_entity_history()
    {
        await LoginAsync();
        var ownerId = await CreateOwnerAsync("Audit Test");
        var horseId = await CreateHorseAsync(ownerId, "AuditHäst");
        var journalId = await CreateAndSignJournalAsync(horseId);

        await _client.GetAsync($"/api/app/journals/{journalId}");
        var list = await _client.GetAsync("/api/app/audit?action=JOURNAL_READ");
        list.EnsureSuccessStatusCode();
        var history = await _client.GetAsync($"/api/app/audit/entity/JournalEntry/{journalId}");
        history.EnsureSuccessStatusCode();
        var items = await history.Content.ReadFromJsonAsync<JsonElement>(Json);
        items.GetArrayLength().ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task Magic_link_token_is_single_use()
    {
        await LoginAsync();
        var ownerId = await CreateOwnerAsync("Magic Link Test", $"magic-{Guid.NewGuid():N}@ex.se");
        using var scope = _factory.Services.CreateScope();
        var tokens = scope.ServiceProvider.GetRequiredService<Equine.Infrastructure.Tokens.PublicBookingTokenService>();
        var token = tokens.Protect(ownerId, DateTimeOffset.UtcNow.AddMinutes(15),
            Equine.Infrastructure.Tokens.PublicBookingTokenService.MagicLinkPurpose);

        var first = await _client.PostAsJsonAsync("/api/public/auth/magic-link/exchange", new { token });
        first.StatusCode.ShouldBe(HttpStatusCode.OK);

        var second = await _client.PostAsJsonAsync("/api/public/auth/magic-link/exchange", new { token });
        second.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Portal_client_cannot_access_other_owner_horse()
    {
        await LoginAsync();
        var ownerA = await CreateOwnerAsync("Portal A", $"a-{Guid.NewGuid():N}@ex.se");
        var ownerB = await CreateOwnerAsync("Portal B", $"b-{Guid.NewGuid():N}@ex.se");
        var horseB = await CreateHorseAsync(ownerB, "Häst B");

        using var scope = _factory.Services.CreateScope();
        var tokens = scope.ServiceProvider.GetRequiredService<Equine.Infrastructure.Tokens.PublicBookingTokenService>();
        var token = tokens.Protect(ownerA, DateTimeOffset.UtcNow.AddMinutes(15),
            Equine.Infrastructure.Tokens.PublicBookingTokenService.MagicLinkPurpose);
        var exchange = await _client.PostAsJsonAsync("/api/public/auth/magic-link/exchange", new { token });
        exchange.EnsureSuccessStatusCode();
        var payload = await exchange.Content.ReadFromJsonAsync<JsonElement>(Json);
        var access = payload.GetProperty("accessToken").GetString()!;

        var portal = _factory.CreateClient();
        portal.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", access);
        var tamper = await portal.GetAsync($"/api/app/portal/horses/{horseB}/summary");
        tamper.StatusCode.ShouldBe(HttpStatusCode.NotFound);
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
        var token = payload.GetProperty("accessToken").GetString();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private async Task<Guid> CreateOwnerAsync(string name, string? email = null)
    {
        email ??= $"owner-{Guid.NewGuid():N}@ex.se";
        var res = await _client.PostAsJsonAsync("/api/app/owners", new
        {
            name,
            email,
            phone = "0700000099"
        });
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<JsonElement>(Json);
        return body.GetProperty("id").GetGuid();
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
        var body = await res.Content.ReadFromJsonAsync<JsonElement>(Json);
        return body.GetProperty("id").GetGuid();
    }

    private async Task<Guid> CreateAndSignJournalAsync(Guid horseId)
    {
        var create = await _client.PostAsJsonAsync("/api/app/journals", new
        {
            horseId,
            performedAt = DateTimeOffset.UtcNow,
            anamnes = "test",
            statusKlinisk = "ok",
            atgarder = "behandling"
        });
        create.EnsureSuccessStatusCode();
        var id = (await create.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetGuid();
        var sign = await _client.PostAsync($"/api/app/journals/{id}/sign", null);
        sign.EnsureSuccessStatusCode();
        return id;
    }
}
