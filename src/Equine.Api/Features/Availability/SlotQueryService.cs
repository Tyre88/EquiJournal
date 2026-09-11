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

    public SlotQueryService(
        EquineDbContext db,
        ISlotCache slotCache,
        IMemoryCache memoryCache,
        ITravelBuffer travelBuffer)
    {
        _db = db;
        _slotCache = slotCache;
        _memoryCache = memoryCache;
        _travelBuffer = travelBuffer;
    }

    public async Task<IReadOnlyList<Slot>> GetSlotsAsync(
        SlotQuery query,
        SlotOptions options,
        CancellationToken cancellationToken = default)
    {
        var version = _memoryCache.Get<int>(MemorySlotCache.VersionKey);
        var key = $"{version}|{query.PractitionerId}|{query.TreatmentTypeId}|{query.From:O}|{query.To:O}|{query.Postcode}|{options.DurationMinutesOverride}|{options.ApplyMinNotice}|{options.ApplyMaxAdvance}|{options.GranularityMinutes}|{options.MaxSlots}";

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
        var slotZones = zones.Select(z => new SlotZone(z.Id, z.GetPostcodes(), z.TravelBufferMinutes, z.IsFallback)).ToList();

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

            Guid? zoneId = null;
            if (slotZones.Count > 0 && !string.IsNullOrWhiteSpace(visit.Location.AddressPostcode))
                zoneId = SlotEngine.ResolveZoneId(visit.Location.AddressPostcode, slotZones);

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
}
