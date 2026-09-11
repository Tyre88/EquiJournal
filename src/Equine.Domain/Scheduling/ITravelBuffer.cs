namespace Equine.Domain.Scheduling;

public interface ITravelBuffer
{
    int MinutesBetween(Guid? fromZoneId, Guid? toZoneId, IReadOnlyList<SlotZone> zones);
}

public sealed class FlatPerZoneTravelBuffer : ITravelBuffer
{
    public int MinutesBetween(Guid? fromZoneId, Guid? toZoneId, IReadOnlyList<SlotZone> zones)
    {
        if (fromZoneId is null || toZoneId is null) return 0;
        if (fromZoneId == toZoneId) return 0;
        var dest = zones.FirstOrDefault(z => z.Id == toZoneId);
        return dest?.TravelBufferMinutes ?? 0;
    }
}
