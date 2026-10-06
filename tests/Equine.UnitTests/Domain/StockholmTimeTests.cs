using Equine.Domain.Scheduling;
using NodaTime;
using Shouldly;
using Xunit;

namespace Equine.UnitTests.Domain;

public class StockholmTimeTests
{
    [Theory]
    [InlineData(2026, 1, 15, 2026, 1, 14, 23)]
    [InlineData(2026, 6, 15, 2026, 6, 14, 22)]
    [InlineData(2026, 3, 29, 2026, 3, 28, 23)]
    [InlineData(2026, 10, 25, 2026, 10, 24, 22)]
    public void Day_bounds_match_stockholm_midnight_used_by_slot_engine(
        int year, int month, int day, int utcYear, int utcMonth, int utcDay, int utcHour)
    {
        var date = new DateOnly(year, month, day);
        var start = StockholmTime.ToUtcStart(date);
        var end = StockholmTime.ToUtcEnd(date);

        start.UtcDateTime.ShouldBe(new DateTime(utcYear, utcMonth, utcDay, utcHour, 0, 0, DateTimeKind.Utc));
        end.ShouldBe(StockholmTime.ToUtcStart(date.AddDays(1)));
        start.ShouldBe(SlotEngineMidnight(date));
        end.ShouldBe(SlotEngineMidnight(date.AddDays(1)));
    }

    [Fact]
    public void Early_swedish_morning_visit_is_inside_the_stockholm_day()
    {
        var day = new DateOnly(2026, 6, 15);
        var start = StockholmTime.ToUtcStart(day);
        var end = StockholmTime.ToUtcEnd(day);
        var visitStart = start.AddMinutes(30);
        var visitEnd = start.AddMinutes(90);

        Overlaps(visitStart, visitEnd, start, end).ShouldBeTrue();
        Overlaps(visitStart, visitEnd, UtcMidnight(day), UtcMidnight(day.AddDays(1))).ShouldBeFalse();
    }

    [Fact]
    public void Winter_visit_in_the_hour_before_utc_midnight_stays_on_that_stockholm_day()
    {
        var day = new DateOnly(2026, 1, 15);
        var start = StockholmTime.ToUtcStart(day);
        var end = StockholmTime.ToUtcEnd(day);
        var visitStart = start.AddMinutes(15);
        var visitEnd = start.AddMinutes(45);

        visitEnd.ShouldBeLessThan(UtcMidnight(day));
        Overlaps(visitStart, visitEnd, start, end).ShouldBeTrue();
        Overlaps(visitStart, visitEnd, UtcMidnight(day), UtcMidnight(day.AddDays(1))).ShouldBeFalse();
    }

    [Fact]
    public void Fully_in_day_visit_stays_inside_the_window()
    {
        var day = new DateOnly(2026, 6, 15);
        var start = StockholmTime.ToUtcStart(day);
        var end = StockholmTime.ToUtcEnd(day);
        var visitStart = start.AddHours(10);
        var visitEnd = start.AddHours(11);

        Overlaps(visitStart, visitEnd, start, end).ShouldBeTrue();
        Overlaps(visitStart, visitEnd, UtcMidnight(day), UtcMidnight(day.AddDays(1))).ShouldBeTrue();
    }

    [Fact]
    public void Next_stockholm_morning_is_outside_today()
    {
        var day = new DateOnly(2026, 6, 15);
        var nextStart = StockholmTime.ToUtcStart(day.AddDays(1));
        var visitStart = nextStart.AddMinutes(30);
        var visitEnd = nextStart.AddMinutes(90);

        Overlaps(visitStart, visitEnd, StockholmTime.ToUtcStart(day), StockholmTime.ToUtcEnd(day)).ShouldBeFalse();
        Overlaps(visitStart, visitEnd, UtcMidnight(day), UtcMidnight(day.AddDays(1))).ShouldBeTrue();
    }

    private static bool Overlaps(DateTimeOffset startsAt, DateTimeOffset endsAt, DateTimeOffset from, DateTimeOffset to) =>
        startsAt < to && endsAt > from;

    private static DateTimeOffset UtcMidnight(DateOnly date) =>
        new(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

    private static DateTimeOffset SlotEngineMidnight(DateOnly date)
    {
        var local = new LocalDate(date.Year, date.Month, date.Day).AtMidnight();
        return local.InZoneLeniently(SlotEngine.Stockholm).ToInstant().ToDateTimeOffset();
    }
}
