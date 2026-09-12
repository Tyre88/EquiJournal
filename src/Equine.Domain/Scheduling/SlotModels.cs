using Equine.Domain.Locations;

namespace Equine.Domain.Scheduling;

public sealed record Slot(DateTimeOffset StartsAt, DateTimeOffset EndsAt);

public sealed record SlotQuery(
    Guid PractitionerId,
    Guid TreatmentTypeId,
    DateOnly From,
    DateOnly To,
    string? Postcode,
    double? Latitude = null,
    double? Longitude = null);

public sealed record SlotOptions(
    int GranularityMinutes = 30,
    int MaxSlots = 200,
    bool ApplyMinNotice = true,
    bool ApplyMaxAdvance = true,
    int? DurationMinutesOverride = null);

public sealed record SlotTreatment(
    int DurationMinutes,
    int BufferBeforeMinutes,
    int BufferAfterMinutes,
    int MinNoticeHours,
    int MaxAdvanceDays);

public sealed record SlotRule(
    DayOfWeek DayOfWeek,
    TimeOnly Start,
    TimeOnly End,
    DateOnly? EffectiveFrom,
    DateOnly? EffectiveTo,
    Guid? ZoneId,
    bool Active);

public sealed record SlotTimeOff(
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    bool AllDay,
    bool RecurringAnnual);

public sealed record OccupyingVisit(
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    int BufferBeforeMinutes,
    int BufferAfterMinutes,
    Guid? ZoneId);

public sealed record SlotZone(
    Guid Id,
    int TravelBufferMinutes,
    bool IsFallback,
    string Name = "",
    string? GeometryKind = null,
    string? GeometryJson = null,
    double? CenterLatitude = null,
    double? CenterLongitude = null,
    double? RadiusKm = null,
    double BufferKm = 0)
{
    public ZoneShape ToShape() => new(
        IsFallback,
        GeometryKind,
        GeometryJson,
        CenterLatitude,
        CenterLongitude,
        RadiusKm,
        BufferKm);

    public ZoneShapeRecord ToRecord() => new(Id, Name, IsFallback, ToShape());
}

public sealed record SlotEngineInput(
    IReadOnlyList<SlotRule> Rules,
    IReadOnlyList<SlotTimeOff> TimeOff,
    IReadOnlyList<OccupyingVisit> Visits,
    IReadOnlyList<SlotZone> Zones,
    SlotTreatment Treatment,
    DateTimeOffset Now);
