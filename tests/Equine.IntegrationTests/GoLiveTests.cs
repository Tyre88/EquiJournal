using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Equine.Infrastructure.Storage;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Equine.IntegrationTests;

public class GoLiveTests : IClassFixture<EquineApiFactory>
{
    private readonly EquineApiFactory _factory;
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public GoLiveTests(EquineApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Health_live_does_not_require_ready_tags()
    {
        var live = await _client.GetAsync("/health");
        live.StatusCode.ShouldBe(HttpStatusCode.OK);
        var ready = await _client.GetAsync("/health/ready");
        ready.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Security_headers_are_present_on_app_responses()
    {
        var response = await _client.GetAsync("/health");
        response.Headers.TryGetValues("X-Content-Type-Options", out var cto).ShouldBeTrue();
        cto!.ShouldContain("nosniff");
        response.Headers.TryGetValues("Referrer-Policy", out var rp).ShouldBeTrue();
        rp!.ShouldContain("strict-origin-when-cross-origin");
    }

    [Fact]
    public async Task Full_archive_contains_signed_journal()
    {
        await LoginAsync();
        var owner = await _client.PostAsJsonAsync("/api/app/owners", new
        {
            name = "Export Ägare",
            email = $"export-{Guid.NewGuid():N}@ex.se",
            phone = "0700000001"
        });
        owner.EnsureSuccessStatusCode();
        var ownerId = (await owner.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetGuid();
        var horse = await _client.PostAsJsonAsync("/api/app/horses", new
        {
            ownerId,
            name = "ExportHäst",
            species = "Hast",
            sex = "Sto",
            birthYear = 2018
        });
        horse.EnsureSuccessStatusCode();
        var horseId = (await horse.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetGuid();
        var journal = await _client.PostAsJsonAsync("/api/app/journals", new
        {
            horseId,
            performedAt = DateTimeOffset.UtcNow,
            anamnes = "export-anamnes-marker",
            statusKlinisk = "ua",
            atgarder = "vila"
        });
        journal.EnsureSuccessStatusCode();
        var journalId = (await journal.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetGuid();
        (await _client.PostAsync($"/api/app/journals/{journalId}/sign", null)).EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var storage = scope.ServiceProvider.GetRequiredService<IObjectStorage>();
        await storage.PutAsync("export-test.txt", new MemoryStream("bilaga"u8.ToArray()), "text/plain");

        var archive = await _client.PostAsync("/api/app/export/archive", null);
        archive.StatusCode.ShouldBe(HttpStatusCode.OK);
        var zipBytes = await archive.Content.ReadAsByteArrayAsync();
        using var zip = new ZipArchive(new MemoryStream(zipBytes), ZipArchiveMode.Read);
        zip.GetEntry("manifest.json").ShouldNotBeNull();
        zip.GetEntry("journals.json").ShouldNotBeNull();
        zip.GetEntry("owners.json").ShouldNotBeNull();
        await using var journals = zip.GetEntry("journals.json")!.Open();
        using var reader = new StreamReader(journals, Encoding.UTF8);
        var text = await reader.ReadToEndAsync();
        text.ShouldContain("export-anamnes-marker");

        var pdfs = await _client.PostAsync("/api/app/export/journals-pdf", null);
        pdfs.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var pdfZip = new ZipArchive(new MemoryStream(await pdfs.Content.ReadAsByteArrayAsync()), ZipArchiveMode.Read);
        pdfZip.Entries.ShouldContain(e => e.Name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase));
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
}
