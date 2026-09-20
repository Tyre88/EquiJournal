using Equine.Domain.Entities;
using Equine.Infrastructure;
using Equine.Infrastructure.Import;
using Equine.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Testcontainers.PostgreSql;
using Xunit;

namespace Equine.IntegrationTests;

public class HistoricImportTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithPassword("testpassword")
        .Build();

    public async Task InitializeAsync() => await _postgres.StartAsync();
    public async Task DisposeAsync() => await _postgres.StopAsync();

    [Fact]
    public async Task Dry_run_writes_nothing()
    {
        var (db, tenant) = await CreateDb();
        await using (db)
        {
            var importer = new HistoricImportService(db, tenant);
            var report = await importer.ImportAsync(SampleOwners(), SampleHorses(), SampleJournals(), commit: false);

            report.DryRun.ShouldBeTrue();
            report.OwnersCreated.ShouldBe(1);
            report.HorsesCreated.ShouldBe(1);
            report.JournalsCreated.ShouldBe(1);
            (await db.Owners.CountAsync()).ShouldBe(0);
            (await db.Horses.CountAsync()).ShouldBe(0);
            (await db.JournalEntries.CountAsync()).ShouldBe(0);
        }
    }

    [Fact]
    public async Task Commit_creates_signed_import_journals()
    {
        var (db, tenant) = await CreateDb();
        await using (db)
        {
            var importer = new HistoricImportService(db, tenant);
            var report = await importer.ImportAsync(
                SampleOwners(), SampleHorses(), SampleJournals(), commit: true, signedByOverride: "Victor (import)");

            report.DryRun.ShouldBeFalse();
            (await db.Owners.CountAsync()).ShouldBe(1);
            (await db.Horses.CountAsync()).ShouldBe(1);
            var journal = await db.JournalEntries.SingleAsync();
            journal.Status.ShouldBe(JournalStatus.Signed);
            journal.Source.ShouldBe(JournalSource.Import);
            journal.SignedBy.ShouldBe("Victor (import)");
            journal.PerformedAt.Year.ShouldBe(2023);
            journal.Anamnes.ShouldBe("stel");
        }
    }

    [Fact]
    public async Task Duplicates_are_skipped_on_second_commit()
    {
        var (db, tenant) = await CreateDb();
        await using (db)
        {
            var importer = new HistoricImportService(db, tenant);
            await importer.ImportAsync(SampleOwners(), SampleHorses(), SampleJournals(), commit: true);
            var second = await importer.ImportAsync(SampleOwners(), SampleHorses(), SampleJournals(), commit: true);

            second.OwnersCreated.ShouldBe(0);
            second.HorsesCreated.ShouldBe(0);
            second.JournalsCreated.ShouldBe(0);
            second.Skipped.Count.ShouldBeGreaterThan(0);
            (await db.Owners.CountAsync()).ShouldBe(1);
            (await db.JournalEntries.CountAsync()).ShouldBe(1);
        }
    }

    private async Task<(EquineDbContext Db, ITenantContext Tenant)> CreateDb()
    {
        var tenant = new TenantContext();
        var services = new ServiceCollection();
        services.AddSingleton<ITenantContext>(tenant);
        services.AddDbContext<EquineDbContext>(o => o.UseNpgsql(_postgres.GetConnectionString()));
        var sp = services.BuildServiceProvider();
        var db = sp.GetRequiredService<EquineDbContext>();
        await db.Database.ExecuteSqlRawAsync("CREATE EXTENSION IF NOT EXISTS citext;");
        await db.Database.ExecuteSqlRawAsync("CREATE EXTENSION IF NOT EXISTS btree_gist;");
        await db.Database.ExecuteSqlRawAsync("CREATE EXTENSION IF NOT EXISTS pg_trgm;");
        await db.Database.EnsureCreatedAsync();
        var seeded = new Tenant("Test", "default");
        db.Tenants.Add(seeded);
        await db.SaveChangesAsync();
        tenant.SetTenant(seeded.Id, seeded.Slug);
        return (db, tenant);
    }

    private static List<IReadOnlyDictionary<string, string>> SampleOwners() =>
    [
        new Dictionary<string, string>
        {
            ["name"] = "Anna",
            ["email"] = "anna@ex.se",
            ["phone"] = "070111",
            ["address_street"] = "A 1",
            ["address_postcode"] = "26100",
            ["address_city"] = "Landskrona",
            ["notes"] = ""
        }
    ];

    private static List<IReadOnlyDictionary<string, string>> SampleHorses() =>
    [
        new Dictionary<string, string>
        {
            ["owner_email"] = "anna@ex.se",
            ["name"] = "Stjärna",
            ["species"] = "Hast",
            ["sex"] = "Sto",
            ["birth_year"] = "2016",
            ["age_group"] = "",
            ["identity"] = "SE1",
            ["breed"] = "",
            ["colour"] = "",
            ["markings"] = "",
            ["stable_city"] = "",
            ["background"] = ""
        }
    ];

    private static List<IReadOnlyDictionary<string, string>> SampleJournals() =>
    [
        new Dictionary<string, string>
        {
            ["owner_email"] = "anna@ex.se",
            ["horse_name"] = "Stjärna",
            ["performed_at"] = "2023-05-12",
            ["treatment_name"] = "Massage",
            ["anamnes"] = "stel",
            ["status_klinisk"] = "öm",
            ["atgarder"] = "massage",
            ["diagnos"] = "",
            ["differentialdiagnoser"] = "",
            ["prognos_och_plan"] = ""
        }
    ];
}
