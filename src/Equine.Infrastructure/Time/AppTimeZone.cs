namespace Equine.Infrastructure.Time;

public static class AppTimeZone
{
    private static readonly TimeZoneInfo Stockholm = TimeZoneInfo.FindSystemTimeZoneById("Europe/Stockholm");

    public static DateTimeOffset ToStockholm(DateTimeOffset utc) => TimeZoneInfo.ConvertTime(utc, Stockholm);
}
