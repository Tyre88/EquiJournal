using Equine.Domain.Entities;
using Equine.Domain.Locations;

namespace Equine.Domain.Scheduling;

public static class SchemaPlaces
{
    public const string HomeKey = "home";
    public const string HomeLabel = "Hemmet";

    public static string? PlaceKey(Location? location, (double Lat, double Lon)? coords)
    {
        if (location is not null && !string.IsNullOrWhiteSpace(location.NormalizedKey) && !location.NormalizedKey.StartsWith("id:", StringComparison.Ordinal))
            return location.NormalizedKey;
        if (location?.Id is { } id)
            return "loc:" + id.ToString("N");
        if (coords is not null)
            return $"{coords.Value.Lat:F4},{coords.Value.Lon:F4}";
        return null;
    }

    public static string StopName(Location? location, BookingLine line)
    {
        if (!string.IsNullOrWhiteSpace(location?.Name)) return location.Name;
        if (!string.IsNullOrWhiteSpace(line.Horse?.StableLocation)) return line.Horse.StableLocation!;
        return line.Owner?.Name ?? "Besök";
    }

    public static string StopAddress(Location? location, BookingLine line)
    {
        if (location is not null)
        {
            var display = AddressNormalization.FormatDisplay(location.AddressStreet, location.AddressPostcode, location.AddressCity);
            if (!string.IsNullOrWhiteSpace(display)) return display;
        }

        var horse = AddressNormalization.FormatDisplay(line.Horse?.StableAddress, line.Horse?.StablePostcode, line.Horse?.StableCity);
        if (!string.IsNullOrWhiteSpace(horse)) return horse;

        return AddressNormalization.FormatDisplay(line.Owner?.AddressStreet, line.Owner?.AddressPostcode, line.Owner?.AddressCity);
    }

    public static IEnumerable<string> HomeQueries(PracticeSettings practice)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var query in new[]
        {
            AddressNormalization.FormatDisplay(practice.AddressStreet, practice.AddressPostcode, practice.AddressCity),
            practice.Address,
            AddressNormalization.FormatDisplay(null, practice.AddressPostcode, practice.AddressCity),
            practice.AddressCity
        })
        {
            var trimmed = query?.Trim() ?? "";
            if (trimmed.Length == 0 || !seen.Add(trimmed)) continue;
            yield return trimmed;
        }
    }

    public static IEnumerable<string> AddressQueries(Location? location, BookingLine line)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var query in CandidateQueries(location, line))
        {
            var trimmed = query.Trim();
            if (trimmed.Length == 0 || !seen.Add(trimmed)) continue;
            yield return trimmed;
        }
    }

    private static IEnumerable<string> CandidateQueries(Location? location, BookingLine line)
    {
        var places = new (string? Street, string? Postcode, string? City)[]
        {
            (location?.AddressStreet, location?.AddressPostcode, location?.AddressCity),
            (line.Horse?.StableAddress, line.Horse?.StablePostcode, line.Horse?.StableCity),
            (line.Owner?.AddressStreet, line.Owner?.AddressPostcode, line.Owner?.AddressCity)
        };

        foreach (var (street, postcode, city) in places)
        {
            var full = JoinAddress(street, postcode, city);
            if (!string.IsNullOrWhiteSpace(full)) yield return full;
        }

        foreach (var (_, postcode, city) in places)
        {
            var town = JoinAddress(postcode, city);
            if (!string.IsNullOrWhiteSpace(town)) yield return town;
            if (!string.IsNullOrWhiteSpace(city)) yield return city!;
        }

        if (!string.IsNullOrWhiteSpace(location?.Name) && location.Name.Length > 3)
        {
            yield return location.Name;
            var namedTown = JoinAddress(location.Name, location.AddressCity);
            if (!string.IsNullOrWhiteSpace(namedTown)) yield return namedTown;
        }
    }

    private static string JoinAddress(params string?[] parts) =>
        string.Join(", ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
}
