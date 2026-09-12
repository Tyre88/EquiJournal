using Equine.Domain.Entities;
using Equine.Domain.Locations;
using Equine.Domain.Scheduling;
using Equine.Infrastructure;
using Equine.Infrastructure.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Equine.Api.Features.Availability;

public sealed class SlotQueryService
{
    public static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);

    private readonly EquineDbContext _db;
    private readonly ISlotCache _slotCache;
    private readonly IMemoryCache _memoryCache;
    private readonly ITravelBuffer _travelBuffer;
    private readonly IPostcodeGeocoder _geocoder;

    public SlotQueryService(
        EquineDbContext db,
        ISlotCache slotCache,
        IMemoryCache memoryCache,
        ITravelBuffer travelBuffer,
        IPostcodeGeocoder geocoder)
    {
        _db = db;
        _slotCache = slotCache;
        _memoryCache = memoryCache;
        _travelBuffer = travelBuffer;
        _geocoder = geocoder;
    }

    public async Task<IReadOnlyList<Slot>> GetSlotsAsync(
        SlotQuery query,
        SlotOptions options,
        CancellationToken cancellationToken = default)
    {
        query = await WithCoordinatesAsync(query, cancellationToken);
        var version = _memoryCache.Get<int>(MemorySlotCache.VersionKey);
        var key = $"{version}|{query.PractitionerId}|{query.TreatmentTypeId}|{query.From:O}|{query.To:O}|{query.Postcode}|{query.Latitude}|{query.Longitude}|{options.DurationMinutesOverride}|{options.ApplyMinNotice}|{options.ApplyMaxAdvance}|{options.GranularityMinutes}|{options.MaxSlots}";

        if (_slotCache.TryGet(key, out var cached) && cached is not null)
            return cached;

        var treatment = await _db.TreatmentTypes.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == query.TreatmentTypeId, cancellationToken);
        if (treatment is null)
            throw new ArgumentException("Treatment type not found.");

        var input = await LoadInputAsync(query, treatment, cancellationToken);
        var slots = SlotEngine.GetAvailableSlots(query, input, options, _travelBuffer);
        _slotCache.Set(key, slots, CacheTtl);
        return slots;
    }

    public async Task<SlotEngineInput> LoadInputAsync(
        SlotQuery query,
        TreatmentType treatment,
        CancellationToken cancellationToken = default)
    {
        var fromUtc = DateTime.SpecifyKind(query.From.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        var toUtc = DateTime.SpecifyKind(query.To.AddDays(1).ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);

        var rules = await _db.AvailabilityRules.AsNoTracking()
            .Where(r => r.PractitionerId == query.PractitionerId && r.Active)
            .ToListAsync(cancellationToken);

        var timeOff = await _db.TimeOff.AsNoTracking()
            .Where(t => t.PractitionerId == query.PractitionerId)
            .ToListAsync(cancellationToken);

        var visits = await _db.Visits
            .AsNoTracking()
            .Include(v => v.Lines)
            .Include(v => v.Location)
            .Where(v => v.PractitionerId == query.PractitionerId && v.OccupiesSlot)
            .Where(v => v.StartsAt < toUtc && v.EndsAt > fromUtc)
            .ToListAsync(cancellationToken);

        var zones = await _db.Zones.AsNoTracking().ToListAsync(cancellationToken);
        var slotZones = zones.Select(ToSlotZone).ToList();

        var treatmentIds = visits
            .SelectMany(v => v.Lines)
            .Select(l => l.TreatmentTypeId)
            .Distinct()
            .ToList();
        var treatments = await _db.TreatmentTypes.AsNoTracking()
            .Where(t => treatmentIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, cancellationToken);

        var occupying = new List<OccupyingVisit>();
        foreach (var visit in visits)
        {
            var active = visit.Lines
                .Where(l => l.Status != BookingStatus.Cancelled)
                .OrderBy(l => l.SortOrder)
                .ToList();
            if (active.Count == 0) continue;

            treatments.TryGetValue(active[0].TreatmentTypeId, out var first);
            treatments.TryGetValue(active[^1].TreatmentTypeId, out var last);

            var zoneId = await ResolveVisitZoneAsync(visit.Location, slotZones, cancellationToken);

            occupying.Add(new OccupyingVisit(
                visit.StartsAt,
                visit.EndsAt,
                first?.BufferBeforeMinutes ?? 0,
                last?.BufferAfterMinutes ?? 0,
                zoneId));
        }

        return new SlotEngineInput(
            rules.Select(r => new SlotRule(r.DayOfWeek, r.StartTime, r.EndTime, r.EffectiveFrom, r.EffectiveTo, r.ZoneId, r.Active)).ToList(),
            timeOff.Select(t => new SlotTimeOff(t.StartsAt, t.EndsAt, t.AllDay, t.RecurringAnnual)).ToList(),
            occupying,
            slotZones,
            new SlotTreatment(
                treatment.DurationMinutes,
                treatment.BufferBeforeMinutes,
                treatment.BufferAfterMinutes,
                treatment.MinNoticeHours,
                treatment.MaxAdvanceDays),
            DateTimeOffset.UtcNow);
    }

    private async Task<SlotQuery> WithCoordinatesAsync(SlotQuery query, CancellationToken cancellationToken)
    {
        if (query.Latitude is not null && query.Longitude is not null)
            return query;
        if (string.IsNullOrWhiteSpace(query.Postcode))
            return query;

        var point = await _geocoder.GeocodeAsync(query.Postcode, cancellationToken);
        return point is null
            ? query
            : query with { Latitude = point.Value.Latitude, Longitude = point.Value.Longitude };
    }

    private async Task<Guid?> ResolveVisitZoneAsync(
        Location location,
        IReadOnlyList<SlotZone> slotZones,
        CancellationToken cancellationToken)
    {
        if (slotZones.Count == 0) return null;

        if (location.Latitude is decimal lat && location.Longitude is decimal lng)
            return SlotEngine.ResolveZoneId((double)lat, (double)lng, slotZones);

        if (string.IsNullOrWhiteSpace(location.AddressPostcode))
            return slotZones.FirstOrDefault(z => z.IsFallback)?.Id;

        var point = await _geocoder.GeocodeAsync(location.AddressPostcode, cancellationToken);
        if (point is null)
            return slotZones.FirstOrDefault(z => z.IsFallback)?.Id;

        return SlotEngine.ResolveZoneId(point.Value.Latitude, point.Value.Longitude, slotZones);
    }

    private static SlotZone ToSlotZone(Zone z) => new(
        z.Id,
        z.TravelBufferMinutes,
        z.IsFallback,
        z.Name,
        z.GeometryKind,
        z.GeometryJson,
        z.CenterLatitude,
        z.CenterLongitude,
        z.RadiusKm,
        z.BufferKm);
}
