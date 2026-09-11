using Equine.Domain.Common;

namespace Equine.Domain.Entities;

public class TimeOff : Entity
{
    public Guid PractitionerId { get; private set; }
    public DateTimeOffset StartsAt { get; private set; }
    public DateTimeOffset EndsAt { get; private set; }
    public bool AllDay { get; private set; }
    public string? Reason { get; private set; }
    public bool RecurringAnnual { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private TimeOff() { }

    public TimeOff(
        Guid practitionerId,
        DateTimeOffset startsAt,
        DateTimeOffset endsAt,
        bool allDay = false,
        string? reason = null,
        bool recurringAnnual = false)
    {
        if (endsAt <= startsAt)
            throw new ArgumentException("End must be after start.");

        PractitionerId = practitionerId;
        StartsAt = startsAt;
        EndsAt = endsAt;
        AllDay = allDay;
        Reason = reason;
        RecurringAnnual = recurringAnnual;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Update(
        DateTimeOffset? startsAt = null,
        DateTimeOffset? endsAt = null,
        bool? allDay = null,
        string? reason = null,
        bool? recurringAnnual = null)
    {
        if (startsAt.HasValue) StartsAt = startsAt.Value;
        if (endsAt.HasValue) EndsAt = endsAt.Value;
        if (EndsAt <= StartsAt)
            throw new ArgumentException("End must be after start.");
        if (allDay.HasValue) AllDay = allDay.Value;
        Reason = reason;
        if (recurringAnnual.HasValue) RecurringAnnual = recurringAnnual.Value;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
