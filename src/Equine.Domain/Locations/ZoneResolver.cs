using Equine.Domain.Entities;

namespace Equine.Domain.Locations;

public static class ZoneResolver
{
    public const string FallbackZoneName = "Övrigt";

    public static Zone Resolve(string? postcode, IReadOnlyList<Zone> zones)
    {
        if (zones.Count == 0)
            throw new InvalidOperationException("At least one zone (Övrigt) must exist.");

        var normalized = AddressNormalization.NormalizePostcode(postcode);
        if (!string.IsNullOrEmpty(normalized))
        {
            var match = zones.FirstOrDefault(z => !z.IsFallback && z.ContainsPostcode(normalized));
            if (match is not null) return match;
        }

        return zones.FirstOrDefault(z => z.IsFallback)
            ?? throw new InvalidOperationException("Fallback zone 'Övrigt' is missing.");
    }
}
