using Equine.Domain.Common;
using Equine.Domain.Locations;

namespace Equine.Domain.Entities;

public class Zone : Entity
{
    public string Name { get; private set; } = string.Empty;
    public string PostcodesJson { get; private set; } = "[]";
    public string? GeometryKind { get; private set; }
    public string? GeometryJson { get; private set; }
    public double? CenterLatitude { get; private set; }
    public double? CenterLongitude { get; private set; }
    public double? RadiusKm { get; private set; }
    public double BufferKm { get; private set; }
    public string? EffectiveGeometryJson { get; private set; }
    public int TravelBufferMinutes { get; private set; }
    public bool IsFallback { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private Zone() { }

    public Zone(string name, int travelBufferMinutes = 0, bool isFallback = false)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        TravelBufferMinutes = travelBufferMinutes;
        IsFallback = isFallback;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public ZoneShape ToShape() => new(
        IsFallback,
        GeometryKind,
        GeometryJson,
        CenterLatitude,
        CenterLongitude,
        RadiusKm,
        BufferKm);

    public bool Contains(double latitude, double longitude) =>
        ZoneGeometry.Contains(ToShape(), latitude, longitude);

    public double EffectiveAreaKm2() => ZoneGeometry.EffectiveAreaKm2(ToShape());

    public void Update(
        string? name = null,
        int? travelBufferMinutes = null,
        double? bufferKm = null)
    {
        if (name is not null) Name = name;
        if (travelBufferMinutes.HasValue) TravelBufferMinutes = travelBufferMinutes.Value;
        if (bufferKm.HasValue)
        {
            BufferKm = Math.Max(0, bufferKm.Value);
            EffectiveGeometryJson = ZoneGeometry.ComputeEffectiveGeoJson(ToShape());
        }

        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SetCircle(double latitude, double longitude, double radiusKm, double bufferKm = 0)
    {
        if (IsFallback)
            throw new InvalidOperationException("Fallback zone cannot have geometry.");
        if (radiusKm <= 0)
            throw new ArgumentOutOfRangeException(nameof(radiusKm));

        GeometryKind = ZoneGeometryKinds.Circle;
        GeometryJson = null;
        CenterLatitude = latitude;
        CenterLongitude = longitude;
        RadiusKm = radiusKm;
        BufferKm = Math.Max(0, bufferKm);
        EffectiveGeometryJson = ZoneGeometry.ComputeEffectiveGeoJson(ToShape());
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SetPolygon(string geometryJson, double bufferKm = 0)
    {
        if (IsFallback)
            throw new InvalidOperationException("Fallback zone cannot have geometry.");
        if (!ZoneGeometry.TryReadPolygon(geometryJson, out _))
            throw new ArgumentException("Ogiltig yta.", nameof(geometryJson));

        GeometryKind = ZoneGeometryKinds.Polygon;
        GeometryJson = geometryJson;
        CenterLatitude = null;
        CenterLongitude = null;
        RadiusKm = null;
        BufferKm = Math.Max(0, bufferKm);
        EffectiveGeometryJson = ZoneGeometry.ComputeEffectiveGeoJson(ToShape());
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void ClearGeometry()
    {
        if (IsFallback) return;
        GeometryKind = null;
        GeometryJson = null;
        CenterLatitude = null;
        CenterLongitude = null;
        RadiusKm = null;
        EffectiveGeometryJson = null;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
