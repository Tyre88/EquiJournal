using System.Text.Json;
using Equine.Domain.Scheduling;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO.Converters;
using NetTopologySuite.Operation.Buffer;

namespace Equine.Domain.Locations;

public static class ZoneGeometryKinds
{
    public const string Polygon = "polygon";
    public const string Circle = "circle";

    public static bool IsKnown(string? kind) =>
        kind is Polygon or Circle;
}

public readonly record struct ZoneShape(
    bool IsFallback,
    string? GeometryKind,
    string? GeometryJson,
    double? CenterLatitude,
    double? CenterLongitude,
    double? RadiusKm,
    double BufferKm);

public static class ZoneGeometry
{
    public const int CirclePolygonSteps = 48;
    private static readonly JsonSerializerOptions GeoJsonOptions = CreateGeoJsonOptions();

    public static bool Contains(ZoneShape shape, double latitude, double longitude)
    {
        if (shape.IsFallback) return false;
        var bufferKm = Math.Max(0, shape.BufferKm);

        if (shape.GeometryKind == ZoneGeometryKinds.Circle
            && shape.CenterLatitude is double cLat
            && shape.CenterLongitude is double cLng
            && shape.RadiusKm is double radius
            && radius > 0)
        {
            var distance = SchemaTravel.HaversineKm(cLat, cLng, latitude, longitude);
            return distance <= radius + bufferKm + 1e-9;
        }

        if (shape.GeometryKind == ZoneGeometryKinds.Polygon
            && TryReadPolygon(shape.GeometryJson, out var polygon))
        {
            var point = new Point(longitude, latitude) { SRID = 4326 };
            if (bufferKm <= 0)
                return polygon.Covers(point);

            var origin = polygon.Centroid;
            var projected = ProjectPolygon(polygon, origin.Y, origin.X);
            var buffered = projected.Buffer(bufferKm * 1000, EndCapStyle.Round);
            var probe = ProjectPoint(latitude, longitude, origin.Y, origin.X);
            return buffered.Covers(probe);
        }

        return false;
    }

    public static double EffectiveAreaKm2(ZoneShape shape)
    {
        if (shape.IsFallback) return double.MaxValue;
        var bufferKm = Math.Max(0, shape.BufferKm);

        if (shape.GeometryKind == ZoneGeometryKinds.Circle
            && shape.RadiusKm is double radius
            && radius > 0)
        {
            var r = radius + bufferKm;
            return Math.PI * r * r;
        }

        if (shape.GeometryKind == ZoneGeometryKinds.Polygon
            && TryReadPolygon(shape.GeometryJson, out var polygon))
        {
            var origin = polygon.Centroid;
            var projected = ProjectPolygon(polygon, origin.Y, origin.X);
            var geometry = bufferKm > 0
                ? projected.Buffer(bufferKm * 1000, EndCapStyle.Round)
                : projected;
            return geometry.Area / 1_000_000d;
        }

        return double.MaxValue;
    }

    public static string? ComputeEffectiveGeoJson(ZoneShape shape)
    {
        if (shape.IsFallback) return null;
        var bufferKm = Math.Max(0, shape.BufferKm);

        if (shape.GeometryKind == ZoneGeometryKinds.Circle
            && shape.CenterLatitude is double cLat
            && shape.CenterLongitude is double cLng
            && shape.RadiusKm is double radius
            && radius > 0)
        {
            return WritePolygon(CirclePolygon(cLat, cLng, radius + bufferKm));
        }

        if (shape.GeometryKind == ZoneGeometryKinds.Polygon
            && TryReadPolygon(shape.GeometryJson, out var polygon))
        {
            if (bufferKm <= 0)
                return WritePolygon(polygon);

            var origin = polygon.Centroid;
            var projected = ProjectPolygon(polygon, origin.Y, origin.X);
            var buffered = projected.Buffer(bufferKm * 1000, EndCapStyle.Round);
            if (buffered is not Polygon bufferedPolygon)
                return WritePolygon(polygon);
            return WritePolygon(UnprojectPolygon(bufferedPolygon, origin.Y, origin.X));
        }

        return null;
    }

    public static string WritePolygon(Polygon polygon)
    {
        polygon.SRID = 4326;
        return JsonSerializer.Serialize(polygon, GeoJsonOptions);
    }

    public static bool TryReadPolygon(string? json, out Polygon polygon)
    {
        polygon = null!;
        if (string.IsNullOrWhiteSpace(json)) return false;
        try
        {
            var geometry = JsonSerializer.Deserialize<Geometry>(json, GeoJsonOptions);
            if (geometry is Polygon poly && poly.IsValid && !poly.IsEmpty)
            {
                polygon = poly;
                return true;
            }

            if (geometry is MultiPolygon multi && multi.NumGeometries > 0)
            {
                var largest = Enumerable.Range(0, multi.NumGeometries)
                    .Select(i => (Polygon)multi.GetGeometryN(i))
                    .OrderByDescending(p => p.Area)
                    .First();
                polygon = largest;
                return largest.IsValid && !largest.IsEmpty;
            }
        }
        catch (JsonException)
        {
            return false;
        }

        return false;
    }

    public static Polygon CirclePolygon(double latitude, double longitude, double radiusKm, int steps = CirclePolygonSteps)
    {
        var ring = new Coordinate[steps + 1];
        for (var i = 0; i < steps; i++)
        {
            var bearing = 360d * i / steps;
            var dest = Destination(latitude, longitude, radiusKm, bearing);
            ring[i] = new Coordinate(dest.Longitude, dest.Latitude);
        }

        ring[steps] = new Coordinate(ring[0].X, ring[0].Y);
        var factory = new GeometryFactory(new PrecisionModel(), 4326);
        return factory.CreatePolygon(factory.CreateLinearRing(ring));
    }

    public static (double Latitude, double Longitude) Destination(
        double latitude,
        double longitude,
        double distanceKm,
        double bearingDegrees)
    {
        const double earthKm = 6371;
        var lat1 = DegreesToRadians(latitude);
        var lon1 = DegreesToRadians(longitude);
        var bearing = DegreesToRadians(bearingDegrees);
        var ang = distanceKm / earthKm;
        var lat2 = Math.Asin(Math.Sin(lat1) * Math.Cos(ang) + Math.Cos(lat1) * Math.Sin(ang) * Math.Cos(bearing));
        var lon2 = lon1 + Math.Atan2(
            Math.Sin(bearing) * Math.Sin(ang) * Math.Cos(lat1),
            Math.Cos(ang) - Math.Sin(lat1) * Math.Sin(lat2));
        return (RadiansToDegrees(lat2), RadiansToDegrees(lon2));
    }

    private static Polygon ProjectPolygon(Polygon polygon, double originLat, double originLng)
    {
        var factory = new GeometryFactory(new PrecisionModel(), 0);
        var shell = ProjectRing(polygon.ExteriorRing, originLat, originLng, factory);
        var holes = new LinearRing[polygon.NumInteriorRings];
        for (var i = 0; i < polygon.NumInteriorRings; i++)
            holes[i] = ProjectRing(polygon.GetInteriorRingN(i), originLat, originLng, factory);
        return factory.CreatePolygon(shell, holes);
    }

    private static LinearRing ProjectRing(LineString ring, double originLat, double originLng, GeometryFactory factory)
    {
        var coords = ring.Coordinates
            .Select(c => ProjectPoint(c.Y, c.X, originLat, originLng).Coordinate)
            .ToArray();
        return factory.CreateLinearRing(coords);
    }

    private static Polygon UnprojectPolygon(Polygon polygon, double originLat, double originLng)
    {
        var factory = new GeometryFactory(new PrecisionModel(), 4326);
        var shell = UnprojectRing(polygon.ExteriorRing, originLat, originLng, factory);
        var holes = new LinearRing[polygon.NumInteriorRings];
        for (var i = 0; i < polygon.NumInteriorRings; i++)
            holes[i] = UnprojectRing(polygon.GetInteriorRingN(i), originLat, originLng, factory);
        return factory.CreatePolygon(shell, holes);
    }

    private static LinearRing UnprojectRing(LineString ring, double originLat, double originLng, GeometryFactory factory)
    {
        var coords = ring.Coordinates
            .Select(c =>
            {
                var (lat, lng) = UnprojectPoint(c.X, c.Y, originLat, originLng);
                return new Coordinate(lng, lat);
            })
            .ToArray();
        return factory.CreateLinearRing(coords);
    }

    private static Point ProjectPoint(double latitude, double longitude, double originLat, double originLng)
    {
        var x = SchemaTravel.HaversineKm(originLat, originLng, originLat, longitude) * 1000
            * Math.Sign(longitude - originLng);
        var y = SchemaTravel.HaversineKm(originLat, originLng, latitude, originLng) * 1000
            * Math.Sign(latitude - originLat);
        return new Point(x, y);
    }

    private static (double Latitude, double Longitude) UnprojectPoint(
        double xMeters,
        double yMeters,
        double originLat,
        double originLng)
    {
        var lat = Destination(originLat, originLng, Math.Abs(yMeters) / 1000d, yMeters >= 0 ? 0 : 180).Latitude;
        var lng = Destination(originLat, originLng, Math.Abs(xMeters) / 1000d, xMeters >= 0 ? 90 : 270).Longitude;
        return (lat, lng);
    }

    private static JsonSerializerOptions CreateGeoJsonOptions()
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        options.Converters.Add(new GeoJsonConverterFactory());
        return options;
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180;
    private static double RadiansToDegrees(double radians) => radians * 180 / Math.PI;
}
