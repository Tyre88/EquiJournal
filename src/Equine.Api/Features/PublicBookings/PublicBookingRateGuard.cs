using Microsoft.Extensions.Caching.Memory;

namespace Equine.Api.Features.PublicBookings;

public sealed class PublicBookingRateGuard
{
    private readonly IMemoryCache _cache;
    private readonly IConfiguration _config;

    public PublicBookingRateGuard(IMemoryCache cache, IConfiguration config)
    {
        _cache = cache;
        _config = config;
    }

    public bool TryAcceptEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return true;
        var limit = _config.GetValue("RateLimiting:PublicBookingEmailPermitLimit", 3);
        var minutes = _config.GetValue("RateLimiting:PublicBookingWindowMinutes", 60);
        var key = $"public-book-email:{email.Trim().ToLowerInvariant()}";
        var count = _cache.GetOrCreate(key, e =>
        {
            e.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(minutes);
            return 0;
        });
        if (count >= limit) return false;
        _cache.Set(key, count + 1, TimeSpan.FromMinutes(minutes));
        return true;
    }
}
