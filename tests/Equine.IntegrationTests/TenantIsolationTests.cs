using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;
using Xunit;

namespace Equine.IntegrationTests;

public class TenantIsolationTests : IClassFixture<EquineApiFactory>
{
    private readonly EquineApiFactory _factory;
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public TenantIsolationTests(EquineApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Register_issues_tokens_with_tenant_claim_and_me_includes_tenant()
    {
        var client = _factory.CreateClient();
        var registered = await RegisterAsync(client, "Solsidan", "solsidan-iso", "solsidan-iso@ex.se");

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(registered.AccessToken);
        jwt.Claims.ShouldContain(c => c.Type == "tenantId" && c.Value == registered.TenantId);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", registered.AccessToken);
        var me = await client.GetFromJsonAsync<JsonElement>("/api/app/me", Json);
        me.GetProperty("tenant").GetProperty("slug").GetString().ShouldBe("solsidan-iso");
        me.GetProperty("tenant").GetProperty("plan").GetString().ShouldBe("Free");
    }

    [Fact]
    public async Task Tenant_cannot_read_another_tenants_owners()
    {
        var client = _factory.CreateClient();
        var a = await RegisterAsync(client, "Klinik A", "klinik-a-iso", "klinik-a-iso@ex.se");
        var b = await RegisterAsync(client, "Klinik B", "klinik-b-iso", "klinik-b-iso@ex.se");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", a.AccessToken);
        var created = await client.PostAsJsonAsync("/api/app/owners", new
        {
            name = "Hemlig ägare",
            email = "hemlig-a@ex.se"
        });
        created.StatusCode.ShouldBe(HttpStatusCode.Created);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", b.AccessToken);
        var owners = await client.GetFromJsonAsync<JsonElement[]>("/api/app/owners", Json);
        owners.ShouldNotBeNull();
        owners!.ShouldNotContain(o => o.GetProperty("email").GetString() == "hemlig-a@ex.se");
    }

    [Fact]
    public async Task Tenant_cannot_read_another_tenants_horses_or_journals()
    {
        var client = _factory.CreateClient();
        var a = await RegisterAsync(client, "Klinik H", "klinik-h-iso", "klinik-h-iso@ex.se");
        var b = await RegisterAsync(client, "Klinik J", "klinik-j-iso", "klinik-j-iso@ex.se");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", a.AccessToken);
        var owner = await client.PostAsJsonAsync("/api/app/owners", new { name = "H-ägare", email = "h-agare@ex.se" });
        owner.EnsureSuccessStatusCode();
        var ownerId = (await owner.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetGuid();
        var horse = await client.PostAsJsonAsync("/api/app/horses", new
        {
            ownerId,
            name = "HemligHäst",
            species = "Hast",
            sex = "Sto",
            birthYear = 2016
        });
        horse.EnsureSuccessStatusCode();
        var horseId = (await horse.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetGuid();
        var journal = await client.PostAsJsonAsync("/api/app/journals", new
        {
            horseId,
            performedAt = DateTimeOffset.UtcNow,
            anamnes = "hemlig",
            statusKlinisk = "ua",
            atgarder = "vila"
        });
        journal.EnsureSuccessStatusCode();
        var journalId = (await journal.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetGuid();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", b.AccessToken);
        var horses = await client.GetFromJsonAsync<JsonElement[]>("/api/app/horses", Json);
        horses.ShouldNotBeNull();
        horses!.ShouldNotContain(h => h.GetProperty("name").GetString() == "HemligHäst");

        (await client.GetAsync($"/api/app/horses/{horseId}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.GetAsync($"/api/app/journals/{journalId}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Public_slug_does_not_see_other_practice_treatments()
    {
        var client = _factory.CreateClient();
        var a = await RegisterAsync(client, "Klinik C", "klinik-c-iso", "klinik-c-iso@ex.se");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", a.AccessToken);
        var treatment = await client.PostAsJsonAsync("/api/app/treatment-types", new
        {
            name = "Privat massage",
            shortDescription = "Endast C",
            durationMinutes = 30,
            bookableOnline = true,
            priceExclVat = 800
        });
        treatment.EnsureSuccessStatusCode();

        var publicA = await client.GetAsync("/api/public/klinik-c-iso/treatments");
        publicA.StatusCode.ShouldBe(HttpStatusCode.OK);
        var bodyA = await publicA.Content.ReadFromJsonAsync<JsonElement>(Json);
        bodyA.GetProperty("treatments").EnumerateArray()
            .ShouldContain(t => t.GetProperty("name").GetString() == "Privat massage");

        var missing = await client.GetAsync("/api/public/does-not-exist/treatments");
        missing.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var other = await client.GetFromJsonAsync<JsonElement>("/api/public/default/treatments", Json);
        other.GetProperty("treatments").EnumerateArray()
            .ShouldNotContain(t => t.GetProperty("name").GetString() == "Privat massage");
    }

    [Fact]
    public async Task Login_jwt_contains_seeded_admin_tenant()
    {
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/app/auth/login", new
        {
            email = "victor@gradera.nu",
            password = "Admin@123456"
        });
        login.EnsureSuccessStatusCode();
        var payload = await login.Content.ReadFromJsonAsync<JsonElement>(Json);
        var token = payload.GetProperty("accessToken").GetString()!;
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        jwt.Claims.ShouldContain(c => c.Type == "tenantId" && !string.IsNullOrWhiteSpace(c.Value));
    }

    private static async Task<Registered> RegisterAsync(HttpClient client, string name, string slug, string email)
    {
        var res = await client.PostAsJsonAsync("/api/public/tenants/register", new
        {
            name,
            slug,
            email,
            password = "Klinik@123456",
            displayName = name
        });
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<JsonElement>(Json);
        return new Registered(
            body.GetProperty("accessToken").GetString()!,
            body.GetProperty("tenant").GetProperty("id").GetGuid().ToString());
    }

    private sealed record Registered(string AccessToken, string TenantId);
}
