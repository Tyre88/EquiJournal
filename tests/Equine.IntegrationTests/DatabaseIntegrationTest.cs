using Equine.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Testcontainers.PostgreSql;
using Xunit;

namespace Equine.IntegrationTests;

public class DatabaseIntegrationTest : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithPassword("testpassword")
        .Build();

    public async Task InitializeAsync() => await _postgres.StartAsync();
    public async Task DisposeAsync() => await _postgres.StopAsync();

    [Fact]
    public async Task EnsureCreated_CreatesAllTables()
    {
        // Arrange
        var connectionString = _postgres.GetConnectionString();

        var services = new ServiceCollection();
        services.AddDbContext<EquineDbContext>(options =>
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null);
            }));
        services.AddIdentity<ApplicationUser, IdentityRole<Guid>>()
            .AddEntityFrameworkStores<EquineDbContext>()
            .AddDefaultTokenProviders();

        var serviceProvider = services.BuildServiceProvider();

        // Act
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EquineDbContext>();
        await context.Database.ExecuteSqlRawAsync("CREATE EXTENSION IF NOT EXISTS citext;");
        await context.Database.ExecuteSqlRawAsync("CREATE EXTENSION IF NOT EXISTS btree_gist;");
        await context.Database.ExecuteSqlRawAsync("CREATE EXTENSION IF NOT EXISTS pg_trgm;");
        await context.Database.EnsureCreatedAsync();

        // Assert
        var tables = await context.Database
            .SqlQueryRaw<string>(@"
                SELECT table_name 
                FROM information_schema.tables 
                WHERE table_schema = 'public'
                AND table_type = 'BASE TABLE'
                ORDER BY table_name")
            .ToListAsync();

        Assert.Contains(tables, t => t.Contains("user", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(tables, t => t.Contains("role", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(tables, t => t.Contains("audit_log", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CanCreateAndRetrieveUser()
    {
        // Arrange
        var connectionString = _postgres.GetConnectionString();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<EquineDbContext>(options =>
            options.UseNpgsql(connectionString));
        services.AddIdentity<ApplicationUser, IdentityRole<Guid>>()
            .AddEntityFrameworkStores<EquineDbContext>()
            .AddDefaultTokenProviders();

        var serviceProvider = services.BuildServiceProvider();

        // Act
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EquineDbContext>();
        await context.Database.ExecuteSqlRawAsync("CREATE EXTENSION IF NOT EXISTS citext;");
        await context.Database.ExecuteSqlRawAsync("CREATE EXTENSION IF NOT EXISTS btree_gist;");
        await context.Database.ExecuteSqlRawAsync("CREATE EXTENSION IF NOT EXISTS pg_trgm;");
        await context.Database.EnsureCreatedAsync();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = new ApplicationUser
        {
            Id = Guid.CreateVersion7(),
            Email = "test@example.se",
            UserName = "test@example.se",
            EmailConfirmed = true,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var result = await userManager.CreateAsync(user, "Test@1234");

        // Assert
        Assert.True(result.Succeeded);

        var found = await userManager.FindByEmailAsync("test@example.se");
        Assert.NotNull(found);
        Assert.Equal("test@example.se", found?.Email);
    }
}
