using Equine.Domain.Scheduling;
using NodaTime;
using Shouldly;
using Xunit;

namespace Equine.UnitTests.Domain;

public class SlotEngineTests
{
    private static readonly DateTimeZone Stockholm = DateTimeZoneProviders.Tzdb["Europe/Stockholm"];
    private static readonly Guid Practitioner = Guid.Parse("11111111-1111-7111-8111-111111111111");
    private static readonly Guid Treatment = Guid.Parse("22222222-2222-7222-8222-222222222222");
    private static readonly Guid ZoneSjobo = Guid.Parse("33333333-3333-7333-8333-333333333333");
    private static readonly Guid ZoneOvrigt = Guid.Parse("44444444-4444-7444-8444-444444444444");

    [Fact]
    public void Booking_buffers_block_adjacent_slots_not_just_duration()
    {
        var monday = new DateOnly(2026, 3, 16);
        var start = Local(monday, 10, 0);
        var visit = new OccupyingVisit(start, start.AddMinutes(30), 15, 15, null);
        var slots = Run(monday, monday, treatment: Treat(30, 15, 15), visits: [visit]);

        LocalTimes(slots, monday).ShouldNotContain(new TimeOnly(9, 30));
        LocalTimes(slots, monday).ShouldNotContain(new TimeOnly(10, 0));
        LocalTimes(slots, monday).ShouldNotContain(new TimeOnly(10, 30));
        LocalTimes(slots, monday).ShouldContain(new TimeOnly(8, 30));
        LocalTimes(slots, monday).ShouldContain(new TimeOnly(11, 0));
    }

    [Fact]
    public void Spring_forward_sunday_has_no_phantom_0200_slot()
    {
        var sunday = new DateOnly(2026, 3, 29);
        var slots = Run(sunday, sunday, rules: [NightRule(DayOfWeek.Sunday)], treatment: Treat(30, 0, 0));
        var locals = slots.Select(LocalOf).ToList();
        locals.ShouldNotContain(l => l.Hour == 2);
        locals.Select(l => l.ToString()).Distinct().Count().ShouldBe(locals.Count);
    }

    [Fact]
    public void Autumn_fallback_sunday_does_not_duplicate_slots()
    {
        var sunday = new DateOnly(2026, 10, 25);
        var slots = Run(sunday, sunday, rules: [NightRule(DayOfWeek.Sunday)], treatment: Treat(30, 0, 0));
        var keys = slots.Select(s => s.StartsAt.UtcTicks).ToList();
        keys.Count.ShouldBe(keys.Distinct().Count());
        slots.Select(s => s.StartsAt).Distinct().Count().ShouldBe(slots.Count);
    }

    [Fact]
    public void Min_notice_excludes_slots_too_soon()
    {
        var day = new DateOnly(2026, 4, 6);
        var now = Local(day, 8, 0);
        var slots = Run(day, day, now: now, treatment: Treat(30, 0, 0, minNoticeHours: 3));
        LocalTimes(slots, day).ShouldNotContain(t => t < new TimeOnly(11, 0));
        LocalTimes(slots, day).ShouldContain(new TimeOnly(11, 0));
    }

    [Fact]
    public void Zone_restricted_day_excludes_wrong_postcode()
    {
        var tuesday = new DateOnly(2026, 3, 17);
        var rules = new[]
        {
            new SlotRule(DayOfWeek.Tuesday, new TimeOnly(8, 0), new TimeOnly(17, 0), null, null, ZoneSjobo, true)
        };
        var zones = Zones();
        var wrong = Run(tuesday, tuesday, rules: rules, zones: zones, postcode: "22100");
        wrong.ShouldBeEmpty();

        var right = Run(tuesday, tuesday, rules: rules, zones: zones, postcode: "27531");
        right.ShouldNotBeEmpty();
    }

    [Fact]
    public void Treatment_longer_than_remaining_window_is_not_offered()
    {
        var day = new DateOnly(2026, 3, 16);
        var rules = new[]
        {
            new SlotRule(DayOfWeek.Monday, new TimeOnly(16, 0), new TimeOnly(17, 0), null, null, null, true)
        };
        var slots = Run(day, day, rules: rules, treatment: Treat(90, 0, 0));
        slots.ShouldBeEmpty();
    }

    [Fact]
    public void All_day_time_off_removes_the_whole_day()
    {
        var day = new DateOnly(2026, 3, 16);
        var start = Local(day, 0, 0);
        var off = new SlotTimeOff(start, start.AddDays(1), AllDay: true, RecurringAnnual: false);
        var slots = Run(day, day, timeOff: [off]);
        slots.ShouldBeEmpty();
    }

    [Fact]
    public void Multi_line_visit_occupies_the_summed_window()
    {
        var day = new DateOnly(2026, 3, 16);
        var start = Local(day, 10, 0);
        var visit = new OccupyingVisit(start, start.AddMinutes(90), 0, 0, null);
        var slots = Run(day, day, visits: [visit], treatment: Treat(30, 0, 0));
        LocalTimes(slots, day).ShouldNotContain(new TimeOnly(10, 0));
        LocalTimes(slots, day).ShouldNotContain(new TimeOnly(10, 30));
        LocalTimes(slots, day).ShouldNotContain(new TimeOnly(11, 0));
        LocalTimes(slots, day).ShouldContain(new TimeOnly(11, 30));
    }

    [Fact]
    public void Same_zone_back_to_back_visits_get_no_extra_travel_buffer()
    {
        var day = new DateOnly(2026, 3, 16);
        var start = Local(day, 10, 0);
        var visit = new OccupyingVisit(start, start.AddMinutes(30), 0, 0, ZoneSjobo);
        var zones = Zones();
        zones[0] = zones[0] with { TravelBufferMinutes = 45 };
        var slots = Run(day, day, visits: [visit], zones: zones, postcode: "27531", treatment: Treat(30, 0, 0));
        LocalTimes(slots, day).ShouldContain(new TimeOnly(10, 30));
    }

    private static IReadOnlyList<Slot> Run(
        DateOnly from,
        DateOnly to,
        DateTimeOffset? now = null,
        SlotTreatment? treatment = null,
        IReadOnlyList<SlotRule>? rules = null,
        IReadOnlyList<SlotTimeOff>? timeOff = null,
        IReadOnlyList<OccupyingVisit>? visits = null,
        IReadOnlyList<SlotZone>? zones = null,
        string? postcode = null)
    {
        var query = new SlotQuery(Practitioner, Treatment, from, to, postcode);
        var input = new SlotEngineInput(
            rules ?? [WeekdayRule(DayOfWeek.Monday), WeekdayRule(DayOfWeek.Tuesday), WeekdayRule(DayOfWeek.Sunday)],
            timeOff ?? [],
            visits ?? [],
            zones ?? Zones(),
            treatment ?? Treat(30, 0, 0),
            now ?? Local(from, 6, 0));
        return SlotEngine.GetAvailableSlots(query, input, new SlotOptions(ApplyMinNotice: treatment?.MinNoticeHours > 0, ApplyMaxAdvance: false));
    }

    private static SlotRule WeekdayRule(DayOfWeek day) =>
        new(day, new TimeOnly(8, 0), new TimeOnly(17, 0), null, null, null, true);

    private static SlotRule NightRule(DayOfWeek day) =>
        new(day, new TimeOnly(1, 0), new TimeOnly(5, 0), null, null, null, true);

    private static SlotTreatment Treat(int duration, int before, int after, int minNoticeHours = 0) =>
        new(duration, before, after, minNoticeHours, 365);

    private static SlotZone[] Zones() =>
    [
        new SlotZone(ZoneSjobo, ["27531"], 0, false),
        new SlotZone(ZoneOvrigt, [], 0, true)
    ];

    private static DateTimeOffset Local(DateOnly date, int hour, int minute)
    {
        var ldt = new LocalDate(date.Year, date.Month, date.Day).At(new LocalTime(hour, minute));
        return ldt.InZoneLeniently(Stockholm).ToDateTimeOffset();
    }

    private static LocalDateTime LocalOf(Slot slot) =>
        Instant.FromDateTimeOffset(slot.StartsAt).InZone(Stockholm).LocalDateTime;

    private static List<TimeOnly> LocalTimes(IReadOnlyList<Slot> slots, DateOnly date) =>
        slots.Select(LocalOf)
            .Where(l => l.Date == new LocalDate(date.Year, date.Month, date.Day))
            .Select(l => new TimeOnly(l.Hour, l.Minute))
            .ToList();
}
