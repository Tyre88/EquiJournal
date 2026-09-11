namespace Equine.Domain.Scheduling;

public enum TravelOrigin
{
    Home,
    PreviousStop
}

public static class SchemaTravel
{
    public static readonly TimeSpan BackToBackMaxGap = TimeSpan.FromMinutes(150);

    public static bool SamePlace(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            return false;
        return string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    public static TravelOrigin Origin(
        DateTimeOffset? previousEndsAt,
        string? previousPlaceKey,
        DateTimeOffset currentStartsAt,
        string? currentPlaceKey)
    {
        if (previousEndsAt is null)
            return TravelOrigin.Home;

        if (SamePlace(previousPlaceKey, currentPlaceKey))
            return TravelOrigin.PreviousStop;

        var gap = currentStartsAt - previousEndsAt.Value;
        return gap <= BackToBackMaxGap ? TravelOrigin.PreviousStop : TravelOrigin.Home;
    }

    public static double HaversineKm(double fromLat, double fromLon, double toLat, double toLon)
    {
        const double earthKm = 6371;
        var dLat = DegreesToRadians(toLat - fromLat);
        var dLon = DegreesToRadians(toLon - fromLon);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
            + Math.Cos(DegreesToRadians(fromLat)) * Math.Cos(DegreesToRadians(toLat))
            * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return earthKm * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    public static int EstimateDriveMinutes(double distanceKm, double kmPerHour = 65)
    {
        if (distanceKm <= 0) return 0;
        return Math.Max(1, (int)Math.Round(distanceKm / kmPerHour * 60));
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180;
}
