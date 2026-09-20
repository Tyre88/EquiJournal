using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Equine.Infrastructure;

public sealed class EquineDbContextFactory : IDesignTimeDbContextFactory<EquineDbContext>
{
    public EquineDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? "Host=localhost;Port=5432;Database=equijournal;Username=postgres;Password=postgres";

        var options = new DbContextOptionsBuilder<EquineDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new EquineDbContext(options);
    }
}
