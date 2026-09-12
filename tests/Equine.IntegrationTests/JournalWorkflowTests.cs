using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Equine.Infrastructure;
using Equine.Infrastructure.Tenancy;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Shouldly;
using Testcontainers.PostgreSql;
using Xunit;

namespace Equine.IntegrationTests;

public class EquineApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithPassword("testpassword")
        .Build();

    public async Task InitializeAsync() => await _postgres.StartAsync();
    public new async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        Environment.SetEnvironmentVariable("ConnectionStrings__Default", _postgres.GetConnectionString());
        Environment.SetEnvironmentVariable("Jwt__SecurityKey", "EquineJournal-SuperSecretJwtKey-2024-ChangeInProduction!");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = _postgres.GetConnectionString(),
                ["Storage:ServiceUrl"] = "http://127.0.0.1:9",
                ["Storage:AccessKey"] = "minioadmin",
                ["Storage:SecretKey"] = "minioadmin",
                ["Storage:Bucket"] = "equine-attachments",
                ["Jwt:SecurityKey"] = "EquineJournal-SuperSecretJwtKey-2024-ChangeInProduction!",
                ["Jwt:Issuer"] = "Equine.Api",
                ["Jwt:Audience"] = "EquineClient",
                ["Jwt:AccessTokenExpiryMinutes"] = "15",
                ["Jwt:RefreshTokenExpiryHours"] = "4",
                ["RateLimiting:PublicReadPermitLimit"] = "1000",
                ["RateLimiting:PublicBookingPermitLimit"] = "1000",
                ["RateLimiting:PublicBookingEmailPermitLimit"] = "1000",
                ["Practitioner:Email"] = "victor@gradera.nu",
                ["Practitioner:Phone"] = "0700000000",
                ["Postmark:ServerToken"] = "test-postmark-token",
                ["Elks:WebhookSecret"] = "test-elks-secret",
                ["Notifications:SendingDomain"] = "localhost",
                ["Admin:Email"] = "victor@gradera.nu",
                ["Admin:Password"] = "Admin@123456",
                ["Admin:DisplayName"] = "Victor"
            });
        });
    }

    public static async Task BindDefaultTenantAsync(IServiceProvider services)
    {
        var tenant = services.GetRequiredService<ITenantContext>();
        if (tenant.HasTenant)
            return;

        var db = services.GetRequiredService<EquineDbContext>();
        var row = await db.Tenants.AsNoTracking().OrderBy(t => t.CreatedAt).FirstAsync();
        tenant.SetTenant(row.Id, row.Slug);
    }

    public async Task<IServiceScope> CreateTenantScopeAsync()
    {
        var scope = Services.CreateScope();
        await BindDefaultTenantAsync(scope.ServiceProvider);
        return scope;
    }
}

public class JournalWorkflowTests : IClassFixture<EquineApiFactory>
{
    private readonly EquineApiFactory _factory;
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public JournalWorkflowTests(EquineApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Signed_journal_cannot_be_edited_or_deleted_and_is_audited()
    {
        await LoginAsync();
        var ownerId = await CreateOwnerAsync();
        var horseId = await CreateHorseAsync(ownerId);
        var journalId = await CreateJournalAsync(horseId, "hälta i vänster fram");

        await PutJournalAsync(journalId, "hälta i vänster fram", "öm", "palpation");
        var sign = await _client.PostAsync($"/api/app/journals/{journalId}/sign", null);
        if (sign.StatusCode != HttpStatusCode.OK)
            throw new HttpRequestException($"Sign failed {(int)sign.StatusCode}: {await sign.Content.ReadAsStringAsync()}");

        var edit = await PutJournalAsync(journalId, "ändrad", "öm", "palpation");
        edit.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var delete = await _client.DeleteAsync($"/api/app/journals/{journalId}");
        delete.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var amend = await _client.PostAsJsonAsync($"/api/app/journals/{journalId}/amendments", new
        {
            text = "Tillägg: labbsvar anlända",
            reason = "Komplettering"
        });
        amend.StatusCode.ShouldBe(HttpStatusCode.Created);

        var read = await _client.GetAsync($"/api/app/journals/{journalId}");
        read.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await read.Content.ReadFromJsonAsync<JsonElement>(Json);
        body.GetProperty("amendments")[0].GetProperty("text").GetString().ShouldContain("labbsvar");

        var from = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(-1).ToString("yyyy-MM-dd"));
        var to = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(1).ToString("yyyy-MM-dd"));
        var range = await _client.GetAsync($"/api/app/journals/by-horse/{horseId}?startDate={from}&endDate={to}&page=1&pageSize=20");
        range.StatusCode.ShouldBe(HttpStatusCode.OK);
        var rangeBody = await range.Content.ReadFromJsonAsync<JsonElement>(Json);
        rangeBody.GetProperty("total").GetInt32().ShouldBeGreaterThan(0);

        var search = await _client.PostAsJsonAsync("/api/app/journals/search", new { query = "hälta" });
        search.StatusCode.ShouldBe(HttpStatusCode.OK);
        var hits = await search.Content.ReadFromJsonAsync<JsonElement>(Json);
        hits.GetArrayLength().ShouldBeGreaterThan(0);

        var pdf = await _client.PostAsync($"/api/app/journals/export/pdf/{journalId}", null);
        pdf.StatusCode.ShouldBe(HttpStatusCode.OK);
        var bytes = await pdf.Content.ReadAsByteArrayAsync();
        Encoding.ASCII.GetString(bytes[..5]).ShouldBe("%PDF-");

        read.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var scope = await _factory.CreateTenantScopeAsync();
        var db = scope.ServiceProvider.GetRequiredService<EquineDbContext>();
        var actions = await db.AuditLog.Select(a => a.Action).ToListAsync();
        actions.ShouldContain("JOURNAL_CREATE");
        actions.ShouldContain("JOURNAL_SIGN");
        actions.ShouldContain("JOURNAL_AMEND");
        actions.ShouldContain("JOURNAL_READ");
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

    private async Task<Guid> CreateOwnerAsync()
    {
        var res = await _client.PostAsJsonAsync("/api/app/owners", new
        {
            name = "Anna Ägare",
            email = $"anna-{Guid.NewGuid():N}@ex.se",
            phone = "0701234567",
            addressStreet = "Stallet 1",
            addressPostcode = "26100",
            addressCity = "Landskrona"
        });
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<JsonElement>(Json);
        return body.GetProperty("id").GetGuid();
    }

    private async Task<Guid> CreateHorseAsync(Guid ownerId)
    {
        var res = await _client.PostAsJsonAsync("/api/app/horses", new
        {
            ownerId,
            name = "Stjärna",
            species = "Hast",
            sex = "Sto",
            birthYear = 2016,
            identity = "SE123"
        });
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<JsonElement>(Json);
        return body.GetProperty("id").GetGuid();
    }

    private async Task<Guid> CreateJournalAsync(Guid horseId, string anamnes)
    {
        var res = await _client.PostAsJsonAsync("/api/app/journals", new
        {
            horseId,
            performedAt = DateTimeOffset.UtcNow,
            anamnes,
            statusKlinisk = "öm vid palpation",
            atgarder = "massage pga hälta"
        });
        if (!res.IsSuccessStatusCode)
            throw new HttpRequestException($"Create journal failed {(int)res.StatusCode}: {await res.Content.ReadAsStringAsync()}");
        var body = await res.Content.ReadFromJsonAsync<JsonElement>(Json);
        return body.GetProperty("id").GetGuid();
    }

    private Task<HttpResponseMessage> PutJournalAsync(Guid id, string anamnes, string status, string atgarder) =>
        _client.PutAsJsonAsync($"/api/app/journals/{id}", new
        {
            anamnes,
            statusKlinisk = status,
            atgarder
        });
}
