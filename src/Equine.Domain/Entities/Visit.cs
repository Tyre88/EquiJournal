using Equine.Domain.Common;

namespace Equine.Domain.Entities;

public class Visit : Entity
{
    public Guid PractitionerId { get; private set; }
    public Guid LocationId { get; private set; }
    public Location Location { get; private set; } = null!;
    public DateTimeOffset StartsAt { get; private set; }
    public DateTimeOffset EndsAt { get; private set; }
    public bool OccupiesSlot { get; private set; } = true;
    public BookingSource Source { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public ICollection<BookingLine> Lines { get; private set; } = new List<BookingLine>();

    private Visit() { }

    public Visit(
        Guid practitionerId,
        Guid locationId,
        DateTimeOffset startsAt,
        BookingSource source = BookingSource.Manual)
    {
        PractitionerId = practitionerId;
        LocationId = locationId;
        StartsAt = startsAt;
        EndsAt = startsAt;
        Source = source;
        OccupiesSlot = true;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void AddLine(BookingLine line)
    {
        Lines.Add(line);
        RecalculateTiming();
    }

    public void RecalculateTiming()
    {
        var minutes = Lines
            .Where(l => l.Status != BookingStatus.Cancelled)
            .Sum(l => l.DurationMinutes);
        EndsAt = StartsAt.AddMinutes(minutes);
        RefreshOccupancy();
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Reschedule(DateTimeOffset startsAt)
    {
        StartsAt = startsAt;
        RecalculateTiming();
    }

    public void SetEndsAt(DateTimeOffset endsAt)
    {
        if (endsAt <= StartsAt)
            throw new ArgumentException("End must be after start.");
        EndsAt = endsAt;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void ChangeLocation(Guid locationId)
    {
        LocationId = locationId;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void RefreshOccupancy()
    {
        OccupiesSlot = Lines.Any(l => l.BlocksSlot);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public DateTimeOffset DeriveLineStart(BookingLine line)
    {
        var offset = Lines
            .Where(l => l.Status != BookingStatus.Cancelled && l.SortOrder < line.SortOrder)
            .Sum(l => l.DurationMinutes);
        return StartsAt.AddMinutes(offset);
    }

    public DateTimeOffset DeriveLineEnd(BookingLine line) =>
        DeriveLineStart(line).AddMinutes(line.DurationMinutes);

    public int NextSortOrder() =>
        Lines.Count == 0 ? 0 : Lines.Max(l => l.SortOrder) + 1;
}
