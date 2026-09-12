using Equine.Domain.Entities;
using Equine.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Shouldly;
using Testcontainers.PostgreSql;
using Xunit;

namespace Equine.IntegrationTests;

public class DatabaseGrantTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithPassword("testpassword")
        .Build();

    public async Task InitializeAsync() => await _postgres.StartAsync();
    public async Task DisposeAsync() => await _postgres.StopAsync();

    [Fact]
    public async Task App_role_cannot_delete_journal_entries()
    {
        var adminCs = _postgres.GetConnectionString();
        var services = new ServiceCollection();
        services.AddDbContext<EquineDbContext>(o => o.UseNpgsql(adminCs));
        await using var sp = services.BuildServiceProvider();
        await using var scope = sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<EquineDbContext>();
        await db.Database.ExecuteSqlRawAsync("CREATE EXTENSION IF NOT EXISTS citext;");
        await db.Database.ExecuteSqlRawAsync("CREATE EXTENSION IF NOT EXISTS btree_gist;");
        await db.Database.ExecuteSqlRawAsync("CREATE EXTENSION IF NOT EXISTS pg_trgm;");
        await db.Database.EnsureCreatedAsync();

        var tenant = new Tenant("Grant", "grant");
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();
        var owner = new Owner("Grant", "grant@ex.se", "0700");
        owner.AssignTenant(tenant.Id);
        var horse = new Horse(owner.Id, "GrantHäst", birthYear: 2015);
        horse.AssignTenant(tenant.Id);
        var journal = new JournalEntry(
            horse.Id, """{"Name":"Grant"}""", "Hast", "Sto", "2015", null,
            DateTimeOffset.UtcNow, "a", "b", "c");
        journal.AssignTenant(tenant.Id);
        journal.Sign("tester");
        db.Owners.Add(owner);
        db.Horses.Add(horse);
        db.JournalEntries.Add(journal);
        await db.SaveChangesAsync();

        await db.Database.ExecuteSqlRawAsync("""
            DO $$
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'equine_app') THEN
                    CREATE ROLE equine_app LOGIN PASSWORD 'app-test-pass';
                END IF;
            END $$;
            GRANT USAGE ON SCHEMA public TO equine_app;
            GRANT SELECT, INSERT, UPDATE ON ALL TABLES IN SCHEMA public TO equine_app;
            REVOKE DELETE, TRUNCATE ON TABLE journal_entries FROM equine_app;
            REVOKE DELETE, TRUNCATE ON TABLE journal_amendments FROM equine_app;
            REVOKE DELETE, TRUNCATE ON TABLE audit_log FROM equine_app;
            """);

        var builder = new NpgsqlConnectionStringBuilder(adminCs)
        {
            Username = "equine_app",
            Password = "app-test-pass"
        };

        await using var conn = new NpgsqlConnection(builder.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand("DELETE FROM journal_entries", conn);
        var ex = await Should.ThrowAsync<PostgresException>(async () => await cmd.ExecuteNonQueryAsync());
        ex.SqlState.ShouldBe(PostgresErrorCodes.InsufficientPrivilege);
    }
}
