using Equine.Domain.Scheduling;

namespace Equine.Infrastructure.Scheduling;

public interface ISlotCache
{
    bool TryGet(string key, out IReadOnlyList<Slot>? slots);
    void Set(string key, IReadOnlyList<Slot> slots, TimeSpan ttl);
    void InvalidateAll();
}
