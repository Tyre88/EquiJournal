namespace Equine.Domain.Notifications;

public static class QuietHours
{
    public static DateTimeOffset DeferToWindow(
        DateTimeOffset scheduledUtc,
        TimeOnly windowStart,
        TimeOnly windowEnd,
        TimeZoneInfo zone)
    {
        var local = TimeZoneInfo.ConvertTime(scheduledUtc, zone);
        var time = TimeOnly.FromDateTime(local.DateTime);
        if (IsInside(time, windowStart, windowEnd))
            return scheduledUtc;

        var date = DateOnly.FromDateTime(local.DateTime);
        if (time < windowStart)
        {
            var next = date.ToDateTime(windowStart);
            return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(next, DateTimeKind.Unspecified), zone);
        }

        var tomorrow = date.AddDays(1).ToDateTime(windowStart);
        return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(tomorrow, DateTimeKind.Unspecified), zone);
    }

    public static bool IsInside(TimeOnly time, TimeOnly start, TimeOnly end)
    {
        if (start <= end)
            return time >= start && time < end;
        return time >= start || time < end;
    }
}
