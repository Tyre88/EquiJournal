using Microsoft.EntityFrameworkCore;

namespace Equine.Infrastructure.Tokens;

public sealed class ConsumedMagicLinkStore
{
    private readonly EquineDbContext _db;

    public ConsumedMagicLinkStore(EquineDbContext db) => _db = db;

    public async Task<bool> IsConsumedAsync(string tokenHash, CancellationToken ct = default) =>
        await _db.Database.SqlQuery<int>($"""
            SELECT COUNT(*)::int AS "Value"
            FROM consumed_magic_link_tokens
            WHERE "TokenHash" = {tokenHash}
            """).FirstOrDefaultAsync(ct) > 0;

    public async Task ConsumeAsync(string tokenHash, CancellationToken ct = default)
    {
        await _db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO consumed_magic_link_tokens ("TokenHash", "ConsumedAt")
            VALUES ({tokenHash}, {DateTimeOffset.UtcNow})
            ON CONFLICT ("TokenHash") DO NOTHING
            """);
    }
}
