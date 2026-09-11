namespace Equine.Domain.Scheduling;

public enum DriveLogPurpose
{
    Business,
    ReturnHome
}

public sealed record DriveLogHome(string Address, double? Latitude, double? Longitude);

public sealed record DriveLogStopInput(
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    string PlaceKey,
    string PlaceName,
    string Address,
    string? OwnerName,
    string? HorseName,
    string? TreatmentName,
    double? Latitude,
    double? Longitude);

public sealed record DriveLogTripDraft(
    DateOnly Date,
    DateTimeOffset StartsAt,
    DriveLogPurpose Purpose,
    string FromLabel,
    string FromAddress,
    string ToLabel,
    string ToAddress,
    string Contacts,
    string Horses,
    string Treatments,
    double? FromLatitude,
    double? FromLongitude,
    double? ToLatitude,
    double? ToLongitude);

public static class DriveLogPlanner
{
    public static IReadOnlyList<DriveLogTripDraft> BuildDay(
        DateOnly date,
        DriveLogHome home,
        IReadOnlyList<DriveLogStopInput> orderedStops)
    {
        var groups = GroupConsecutive(orderedStops);
        if (groups.Count == 0) return [];

        var trips = new List<DriveLogTripDraft>();
        DriveLogStopGroup? previous = null;

        foreach (var group in groups)
        {
            var origin = SchemaTravel.Origin(
                previous?.EndsAt,
                previous?.PlaceKey,
                group.StartsAt,
                group.PlaceKey);

            if (origin == TravelOrigin.Home && previous is not null)
            {
                var homeTrip = ReturnHome(date, previous.EndsAt, previous, home);
                if (homeTrip is not null) trips.Add(homeTrip);
            }

            var fromHome = origin == TravelOrigin.Home || previous is null;
            var trip = BusinessTrip(date, group, fromHome, previous, home);
            if (trip is not null) trips.Add(trip);

            previous = group;
        }

        if (previous is not null)
        {
            var homeTrip = ReturnHome(date, previous.EndsAt, previous, home);
            if (homeTrip is not null) trips.Add(homeTrip);
        }

        return trips;
    }

    private static DriveLogTripDraft? BusinessTrip(
        DateOnly date,
        DriveLogStopGroup group,
        bool fromHome,
        DriveLogStopGroup? previous,
        DriveLogHome home)
    {
        if (previous is not null && SchemaTravel.SamePlace(previous.PlaceKey, group.PlaceKey))
            return null;

        var fromLabel = fromHome || previous is null ? SchemaPlaces.HomeLabel : previous.PlaceName;
        var fromAddress = fromHome || previous is null ? home.Address : previous.Address;
        var fromLat = fromHome || previous is null ? home.Latitude : previous.Latitude;
        var fromLon = fromHome || previous is null ? home.Longitude : previous.Longitude;

        if (SameCoords(fromLat, fromLon, group.Latitude, group.Longitude)
            && string.Equals(fromAddress, group.Address, StringComparison.OrdinalIgnoreCase))
            return null;

        return new DriveLogTripDraft(
            date,
            group.StartsAt,
            DriveLogPurpose.Business,
            fromLabel,
            fromAddress,
            group.PlaceName,
            group.Address,
            JoinUnique(group.Owners),
            JoinUnique(group.Horses),
            JoinUnique(group.Treatments),
            fromLat,
            fromLon,
            group.Latitude,
            group.Longitude);
    }

    private static DriveLogTripDraft? ReturnHome(
        DateOnly date,
        DateTimeOffset startsAt,
        DriveLogStopGroup from,
        DriveLogHome home)
    {
        if (SameCoords(from.Latitude, from.Longitude, home.Latitude, home.Longitude)
            && string.Equals(from.Address, home.Address, StringComparison.OrdinalIgnoreCase))
            return null;

        return new DriveLogTripDraft(
            date,
            startsAt,
            DriveLogPurpose.ReturnHome,
            from.PlaceName,
            from.Address,
            SchemaPlaces.HomeLabel,
            home.Address,
            "",
            "",
            "",
            from.Latitude,
            from.Longitude,
            home.Latitude,
            home.Longitude);
    }

    private static List<DriveLogStopGroup> GroupConsecutive(IReadOnlyList<DriveLogStopInput> orderedStops)
    {
        var groups = new List<DriveLogStopGroup>();
        foreach (var stop in orderedStops)
        {
            var last = groups.Count > 0 ? groups[^1] : null;
            if (last is not null && SchemaTravel.SamePlace(last.PlaceKey, stop.PlaceKey))
            {
                last.Add(stop);
                continue;
            }

            groups.Add(DriveLogStopGroup.Start(stop));
        }

        return groups;
    }

    private static bool SameCoords(double? aLat, double? aLon, double? bLat, double? bLon) =>
        aLat is not null && aLon is not null && bLat is not null && bLon is not null
        && Math.Abs(aLat.Value - bLat.Value) < 0.0001
        && Math.Abs(aLon.Value - bLon.Value) < 0.0001;

    private static string JoinUnique(IEnumerable<string> values) =>
        string.Join(", ", values
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.OrdinalIgnoreCase));

    private sealed class DriveLogStopGroup
    {
        public DateTimeOffset StartsAt { get; private set; }
        public DateTimeOffset EndsAt { get; private set; }
        public string PlaceKey { get; }
        public string PlaceName { get; }
        public string Address { get; }
        public double? Latitude { get; }
        public double? Longitude { get; }
        public List<string> Owners { get; } = [];
        public List<string> Horses { get; } = [];
        public List<string> Treatments { get; } = [];

        private DriveLogStopGroup(DriveLogStopInput stop)
        {
            StartsAt = stop.StartsAt;
            EndsAt = stop.EndsAt;
            PlaceKey = stop.PlaceKey;
            PlaceName = stop.PlaceName;
            Address = stop.Address;
            Latitude = stop.Latitude;
            Longitude = stop.Longitude;
            Add(stop);
        }

        public static DriveLogStopGroup Start(DriveLogStopInput stop) => new(stop);

        public void Add(DriveLogStopInput stop)
        {
            if (stop.StartsAt < StartsAt) StartsAt = stop.StartsAt;
            if (stop.EndsAt > EndsAt) EndsAt = stop.EndsAt;
            if (!string.IsNullOrWhiteSpace(stop.OwnerName)) Owners.Add(stop.OwnerName);
            if (!string.IsNullOrWhiteSpace(stop.HorseName)) Horses.Add(stop.HorseName);
            if (!string.IsNullOrWhiteSpace(stop.TreatmentName)) Treatments.Add(stop.TreatmentName);
        }
    }
}
