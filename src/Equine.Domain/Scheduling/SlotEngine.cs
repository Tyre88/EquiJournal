using Equine.Domain.Locations;
using NodaTime;

namespace Equine.Domain.Scheduling;

public static class SlotEngine
{
    public static readonly DateTimeZone Stockholm = DateTimeZoneProviders.Tzdb["Europe/Stockholm"];

    public static IReadOnlyList<Slot> GetAvailableSlots(
        SlotQuery query,
        SlotEngineInput input,
        SlotOptions? options = null,
        ITravelBuffer? travelBuffer = null)
    {
        options ??= new SlotOptions();
        travelBuffer ??= new FlatPerZoneTravelBuffer();

        if (query.To < query.From) return [];
        if (options.GranularityMinutes <= 0) throw new ArgumentOutOfRangeException(nameof(options));

        var duration = options.DurationMinutesOverride ?? input.Treatment.DurationMinutes;
        if (duration <= 0) return [];

        Guid? queryZoneId = ResolveQueryZone(query, input.Zones);

        var nowInstant = Instant.FromDateTimeOffset(input.Now);
        var minStart = options.ApplyMinNotice
            ? nowInstant.Plus(Duration.FromHours(input.Treatment.MinNoticeHours))
            : Instant.MinValue;
        var maxStart = options.ApplyMaxAdvance
            ? nowInstant.Plus(Duration.FromDays(input.Treatment.MaxAdvanceDays))
            : Instant.MaxValue;

        var blocked = BuildBlockedIntervals(input, query, queryZoneId, travelBuffer);
        var slots = new List<Slot>();

        for (var date = query.From; date <= query.To; date = date.AddDays(1))
        {
            var windows = ExpandDayWindows(date, query, input, queryZoneId);
            foreach (var window in windows)
            {
                SubtractAndSlice(
                    window,
                    blocked,
                    duration,
                    input.Treatment.BufferBeforeMinutes,
                    input.Treatment.BufferAfterMinutes,
                    options.GranularityMinutes,
                    minStart,
                    maxStart,
                    slots,
                    options.MaxSlots);

                if (slots.Count >= options.MaxSlots)
                    return slots;
            }
        }

        return slots;
    }

    public static Guid? ResolveQueryZone(SlotQuery query, IReadOnlyList<SlotZone> zones)
    {
        if (zones.Count == 0) return null;
        if (query.Latitude is double lat && query.Longitude is double lng)
            return ResolveZoneId(lat, lng, zones);
        if (!string.IsNullOrWhiteSpace(query.Postcode))
            return zones.FirstOrDefault(z => z.IsFallback)?.Id;
        return null;
    }

    public static Guid? ResolveZoneId(double latitude, double longitude, IReadOnlyList<SlotZone> zones) =>
        ZoneResolver.ResolveId(latitude, longitude, zones.Select(z => z.ToRecord()).ToList());

    private static List<(Instant Start, Instant End)> ExpandDayWindows(
        DateOnly date,
        SlotQuery query,
        SlotEngineInput input,
        Guid? queryZoneId)
    {
        var localDate = new LocalDate(date.Year, date.Month, date.Day);
        var rules = input.Rules
            .Where(r => r.Active)
            .Where(r => r.DayOfWeek == date.DayOfWeek)
            .Where(r => r.EffectiveFrom is null || date >= r.EffectiveFrom)
            .Where(r => r.EffectiveTo is null || date <= r.EffectiveTo)
            .ToList();

        if (rules.Count == 0) return [];

        if (queryZoneId is not null)
        {
            rules = rules
                .Where(r => r.ZoneId is null || r.ZoneId == queryZoneId)
                .ToList();
            if (rules.Count == 0) return [];
        }

        var windows = new List<(Instant Start, Instant End)>();
        foreach (var rule in rules)
        {
            var start = LocalToInstant(localDate, rule.Start);
            var end = LocalToInstant(localDate, rule.End);
            if (start is null || end is null || end <= start) continue;
            windows.Add((start.Value, end.Value));
        }

        return windows;
    }

    private static Instant? LocalToInstant(LocalDate date, TimeOnly time)
    {
        var ldt = date + new LocalTime(time.Hour, time.Minute, time.Second);
        var mapping = Stockholm.MapLocal(ldt);
        if (mapping.Count == 0) return null;
        return mapping.First().ToInstant();
    }

    private static List<(Instant Start, Instant End)> BuildBlockedIntervals(
        SlotEngineInput input,
        SlotQuery query,
        Guid? queryZoneId,
        ITravelBuffer travelBuffer)
    {
        var blocked = new List<(Instant Start, Instant End)>();

        foreach (var off in input.TimeOff)
            blocked.AddRange(ExpandTimeOff(off, query.From, query.To));

        foreach (var visit in input.Visits)
        {
            var travel = travelBuffer.MinutesBetween(visit.ZoneId, queryZoneId, input.Zones);
            var start = Instant.FromDateTimeOffset(visit.StartsAt)
                .Minus(Duration.FromMinutes(visit.BufferBeforeMinutes + travel));
            var end = Instant.FromDateTimeOffset(visit.EndsAt)
                .Plus(Duration.FromMinutes(visit.BufferAfterMinutes + travel));
            if (end > start)
                blocked.Add((start, end));
        }

        return blocked;
    }

    private static IEnumerable<(Instant Start, Instant End)> ExpandTimeOff(
        SlotTimeOff off,
        DateOnly rangeFrom,
        DateOnly rangeTo)
    {
        if (!off.RecurringAnnual)
        {
            yield return AllDayOrRange(off);
            yield break;
        }

        var templateStart = Instant.FromDateTimeOffset(off.StartsAt).InZone(Stockholm).Date;
        var month = templateStart.Month;
        var day = templateStart.Day;

        for (var year = rangeFrom.Year - 1; year <= rangeTo.Year + 1; year++)
        {
            if (day > CalendarSystem.Iso.GetDaysInMonth(year, month)) continue;
            var anniversary = new LocalDate(year, month, day);
            if (off.AllDay)
            {
                var start = anniversary.AtMidnight().InZoneLeniently(Stockholm).ToInstant();
                var end = anniversary.PlusDays(1).AtMidnight().InZoneLeniently(Stockholm).ToInstant();
                yield return (start, end);
            }
            else
            {
                var localStart = Instant.FromDateTimeOffset(off.StartsAt).InZone(Stockholm).TimeOfDay;
                var localEnd = Instant.FromDateTimeOffset(off.EndsAt).InZone(Stockholm).TimeOfDay;
                var start = anniversary.At(localStart).InZoneLeniently(Stockholm).ToInstant();
                var endDate = localEnd <= localStart ? anniversary.PlusDays(1) : anniversary;
                var end = endDate.At(localEnd).InZoneLeniently(Stockholm).ToInstant();
                yield return (start, end);
            }
        }
    }

    private static (Instant Start, Instant End) AllDayOrRange(SlotTimeOff off)
    {
        if (!off.AllDay)
            return (Instant.FromDateTimeOffset(off.StartsAt), Instant.FromDateTimeOffset(off.EndsAt));

        var local = Instant.FromDateTimeOffset(off.StartsAt).InZone(Stockholm).Date;
        var start = local.AtMidnight().InZoneLeniently(Stockholm).ToInstant();
        var end = local.PlusDays(1).AtMidnight().InZoneLeniently(Stockholm).ToInstant();
        return (start, end);
    }

    private static void SubtractAndSlice(
        (Instant Start, Instant End) window,
        List<(Instant Start, Instant End)> blocked,
        int durationMinutes,
        int bufferBefore,
        int bufferAfter,
        int granularityMinutes,
        Instant minStart,
        Instant maxStart,
        List<Slot> slots,
        int maxSlots)
    {
        var first = window.Start.Plus(Duration.FromMinutes(bufferBefore));
        var lastEnd = window.End.Minus(Duration.FromMinutes(bufferAfter));

        var cursor = AlignToGranularity(first, granularityMinutes);
        var step = Duration.FromMinutes(granularityMinutes);
        var duration = Duration.FromMinutes(durationMinutes);

        while (cursor.Plus(duration) <= lastEnd)
        {
            if (slots.Count >= maxSlots) return;

            var slotStart = cursor;
            var slotEnd = cursor.Plus(duration);
            var paddedStart = slotStart.Minus(Duration.FromMinutes(bufferBefore));
            var paddedEnd = slotEnd.Plus(Duration.FromMinutes(bufferAfter));

            if (slotStart >= minStart && slotStart <= maxStart
                && paddedStart >= window.Start && paddedEnd <= window.End
                && !OverlapsAny(paddedStart, paddedEnd, blocked)
                && IsRepresentableLocalStart(slotStart))
            {
                slots.Add(new Slot(slotStart.ToDateTimeOffset(), slotEnd.ToDateTimeOffset()));
            }

            var next = cursor.Plus(step);
            if (next <= cursor) break;
            cursor = next;
        }
    }

    private static Instant AlignToGranularity(Instant instant, int granularityMinutes)
    {
        var zoned = instant.InZone(Stockholm);
        var minutes = zoned.Minute;
        var rem = minutes % granularityMinutes;
        if (rem == 0 && zoned.Second == 0 && zoned.NanosecondOfSecond == 0)
            return instant;

        var add = granularityMinutes - rem;
        var alignedLocal = zoned.LocalDateTime.PlusMinutes(add)
            .PlusSeconds(-zoned.Second)
            .PlusNanoseconds(-zoned.NanosecondOfSecond);
        var mapping = Stockholm.MapLocal(alignedLocal);
        if (mapping.Count == 0)
        {
            var afterGap = alignedLocal.PlusMinutes(granularityMinutes);
            mapping = Stockholm.MapLocal(afterGap);
            if (mapping.Count == 0) return instant;
        }

        return mapping.First().ToInstant();
    }

    private static bool IsRepresentableLocalStart(Instant instant)
    {
        var zoned = instant.InZone(Stockholm);
        var mapping = Stockholm.MapLocal(zoned.LocalDateTime);
        return mapping.Count > 0;
    }

    private static bool OverlapsAny(Instant start, Instant end, List<(Instant Start, Instant End)> blocked) =>
        blocked.Any(b => start < b.End && end > b.Start);
}
