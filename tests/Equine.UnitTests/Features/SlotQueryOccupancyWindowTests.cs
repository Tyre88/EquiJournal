using Equine.Api.Features.Availability;
using Shouldly;
using Xunit;

namespace Equine.UnitTests.Features;

public class SlotQueryOccupancyWindowTests
{
    [Fact]
    public void Summer_window_includes_a_visit_in_the_first_stockholm_hours()
    {
        var day = new DateOnly(2026, 6, 15);
        var start = SlotQueryService.ToUtcStart(day);
        var end = SlotQueryService.ToUtcEnd(day);

        start.ShouldBe(Utc(2026, 6, 14, 22, 0));
        end.ShouldBe(Utc(2026, 6, 15, 22, 0));

        var visitStart = Utc(2026, 6, 14, 22, 30);
        var visitEnd = Utc(2026, 6, 14, 23, 30);
        Overlaps(visitStart, visitEnd, start, end).ShouldBeTrue();

        var utcMidnight = Utc(2026, 6, 15, 0, 0);
        var utcNextMidnight = Utc(2026, 6, 16, 0, 0);
        Overlaps(visitStart, visitEnd, utcMidnight, utcNextMidnight).ShouldBeFalse();
    }

    [Fact]
    public void Winter_window_includes_a_visit_in_the_first_stockholm_hour()
    {
        var day = new DateOnly(2026, 1, 15);
        var start = SlotQueryService.ToUtcStart(day);
        var end = SlotQueryService.ToUtcEnd(day);

        start.ShouldBe(Utc(2026, 1, 14, 23, 0));
        end.ShouldBe(Utc(2026, 1, 15, 23, 0));

        var visitStart = Utc(2026, 1, 14, 23, 15);
        var visitEnd = Utc(2026, 1, 14, 23, 45);
        Overlaps(visitStart, visitEnd, start, end).ShouldBeTrue();

        var utcMidnight = Utc(2026, 1, 15, 0, 0);
        Overlaps(visitStart, visitEnd, utcMidnight, utcMidnight.AddDays(1)).ShouldBeFalse();
    }

    [Fact]
    public void Range_end_is_exclusive_stockholm_midnight_after_the_last_day()
    {
        var end = SlotQueryService.ToUtcEnd(new DateOnly(2026, 6, 16));
        end.ShouldBe(Utc(2026, 6, 16, 22, 0));
    }

    private static bool Overlaps(DateTimeOffset visitStart, DateTimeOffset visitEnd, DateTimeOffset from, DateTimeOffset to) =>
        visitStart < to && visitEnd > from;

    private static DateTimeOffset Utc(int year, int month, int day, int hour, int minute) =>
        new(year, month, day, hour, minute, 0, TimeSpan.Zero);
}
