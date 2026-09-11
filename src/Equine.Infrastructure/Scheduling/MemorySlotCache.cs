using Equine.Domain.Scheduling;
using Microsoft.Extensions.Caching.Memory;

namespace Equine.Infrastructure.Scheduling;

public sealed class MemorySlotCache : ISlotCache
{
    public const string VersionKey = "slots:version";
    private readonly IMemoryCache _cache;

    public MemorySlotCache(IMemoryCache cache) => _cache = cache;

    public bool TryGet(string key, out IReadOnlyList<Slot>? slots)
    {
        if (_cache.TryGetValue(key, out IReadOnlyList<Slot>? cached) && cached is not null)
        {
            slots = cached;
            return true;
        }

        slots = null;
        return false;
    }

    public void Set(string key, IReadOnlyList<Slot> slots, TimeSpan ttl) =>
        _cache.Set(key, slots, ttl);

    public void InvalidateAll()
    {
        var version = _cache.Get<int>(VersionKey);
        _cache.Set(VersionKey, version + 1);
    }
}
