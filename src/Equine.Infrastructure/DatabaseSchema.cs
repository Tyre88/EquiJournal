using Microsoft.EntityFrameworkCore;

namespace Equine.Infrastructure;

public static class DatabaseSchema
{
    public static async Task MigrateAsync(EquineDbContext db, CancellationToken cancellationToken = default) =>
        await db.Database.MigrateAsync(cancellationToken);
}
