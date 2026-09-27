namespace Equine.Domain.Scheduling;

public static class StockholmTime
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Stockholm");

    public static DateTimeOffset ToUtcStart(DateOnly date)
    {
        var local = date.ToDateTime(TimeOnly.MinValue);
        return new DateTimeOffset(local, Zone.GetUtcOffset(local)).ToUniversalTime();
    }

    public static DateTimeOffset ToUtcEnd(DateOnly date) => ToUtcStart(date.AddDays(1));
}
