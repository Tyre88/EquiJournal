using Equine.Domain.Common;

namespace Equine.Domain.Entities;

public class AvailabilityRule : TenantScopedEntity
{
    public Guid PractitionerId { get; private set; }
    public DayOfWeek DayOfWeek { get; private set; }
    public TimeOnly StartTime { get; private set; }
    public TimeOnly EndTime { get; private set; }
    public DateOnly? EffectiveFrom { get; private set; }
    public DateOnly? EffectiveTo { get; private set; }
    public Guid? ZoneId { get; private set; }
    public Zone? Zone { get; private set; }
    public bool Active { get; private set; } = true;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private AvailabilityRule() { }

    public AvailabilityRule(
        Guid practitionerId,
        DayOfWeek dayOfWeek,
        TimeOnly startTime,
        TimeOnly endTime,
        DateOnly? effectiveFrom = null,
        DateOnly? effectiveTo = null,
        Guid? zoneId = null,
        bool active = true)
    {
        if (endTime <= startTime)
            throw new ArgumentException("End time must be after start time.");

        PractitionerId = practitionerId;
        DayOfWeek = dayOfWeek;
        StartTime = startTime;
        EndTime = endTime;
        EffectiveFrom = effectiveFrom;
        EffectiveTo = effectiveTo;
        ZoneId = zoneId;
        Active = active;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Update(
        DayOfWeek? dayOfWeek = null,
        TimeOnly? startTime = null,
        TimeOnly? endTime = null,
        DateOnly? effectiveFrom = null,
        DateOnly? effectiveTo = null,
        Guid? zoneId = null,
        bool clearZone = false,
        bool? active = null)
    {
        if (dayOfWeek.HasValue) DayOfWeek = dayOfWeek.Value;
        if (startTime.HasValue) StartTime = startTime.Value;
        if (endTime.HasValue) EndTime = endTime.Value;
        if (EndTime <= StartTime)
            throw new ArgumentException("End time must be after start time.");
        EffectiveFrom = effectiveFrom;
        EffectiveTo = effectiveTo;
        if (clearZone) ZoneId = null;
        else if (zoneId.HasValue) ZoneId = zoneId;
        if (active.HasValue) Active = active.Value;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public bool AppliesOn(DateOnly date)
    {
        if (!Active) return false;
        if (DayOfWeek != date.DayOfWeek) return false;
        if (EffectiveFrom is DateOnly from && date < from) return false;
        if (EffectiveTo is DateOnly to && date > to) return false;
        return true;
    }
}
