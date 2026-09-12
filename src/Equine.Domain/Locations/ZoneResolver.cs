using Equine.Domain.Entities;

namespace Equine.Domain.Locations;

public static class ZoneResolver
{
    public const string FallbackZoneName = "Övrigt";

    public static Zone Resolve(double? latitude, double? longitude, IReadOnlyList<Zone> zones)
    {
        if (zones.Count == 0)
            throw new InvalidOperationException("At least one zone (Övrigt) must exist.");

        var fallback = zones.FirstOrDefault(z => z.IsFallback)
            ?? throw new InvalidOperationException("Fallback zone 'Övrigt' is missing.");

        if (latitude is null || longitude is null)
            return fallback;

        var matches = zones
            .Where(z => !z.IsFallback && z.Contains(latitude.Value, longitude.Value))
            .OrderBy(z => z.EffectiveAreaKm2())
            .ThenBy(z => z.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return matches.FirstOrDefault() ?? fallback;
    }

    public static Guid? ResolveId(double? latitude, double? longitude, IReadOnlyList<ZoneShapeRecord> zones)
    {
        if (zones.Count == 0) return null;
        var fallback = zones.FirstOrDefault(z => z.IsFallback);

        if (latitude is null || longitude is null)
            return fallback?.Id;

        var match = zones
            .Where(z => !z.IsFallback && ZoneGeometry.Contains(z.Shape, latitude.Value, longitude.Value))
            .OrderBy(z => ZoneGeometry.EffectiveAreaKm2(z.Shape))
            .ThenBy(z => z.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        return match?.Id ?? fallback?.Id;
    }
}

public sealed record ZoneShapeRecord(Guid Id, string Name, bool IsFallback, ZoneShape Shape);
