using Equine.Domain.Entities;
using Equine.Domain.Locations;
using Shouldly;
using Xunit;

namespace Equine.UnitTests.Domain;

public class ZoneResolverTests
{
    private const double SjoboLat = 55.6318;
    private const double SjoboLng = 13.7039;
    private const double LundLat = 55.7047;
    private const double LundLng = 13.1910;

    [Fact]
    public void Point_inside_polygon_resolves_named_zone()
    {
        var sjobo = PolygonAround("Sjöbo", SjoboLat, SjoboLng, 8);
        var fallback = new Zone("Övrigt", isFallback: true);
        ZoneResolver.Resolve(SjoboLat, SjoboLng, [sjobo, fallback]).Name.ShouldBe("Sjöbo");
    }

    [Fact]
    public void Point_outside_polygon_but_inside_buffer_resolves_named_zone()
    {
        var sjobo = PolygonAround("Sjöbo", SjoboLat, SjoboLng, 1, bufferKm: 8);
        var fallback = new Zone("Övrigt", isFallback: true);
        var justOutside = ZoneGeometry.Destination(SjoboLat, SjoboLng, 3, 0);
        ZoneResolver.Resolve(justOutside.Latitude, justOutside.Longitude, [sjobo, fallback]).Name.ShouldBe("Sjöbo");
    }

    [Fact]
    public void Point_outside_buffer_falls_back_to_ovrigt()
    {
        var sjobo = PolygonAround("Sjöbo", SjoboLat, SjoboLng, 2, bufferKm: 1);
        var fallback = new Zone("Övrigt", isFallback: true);
        ZoneResolver.Resolve(LundLat, LundLng, [sjobo, fallback]).IsFallback.ShouldBeTrue();
    }

    [Fact]
    public void Circle_includes_point_within_radius_plus_buffer()
    {
        var sjobo = CircleAround("Sjöbo", SjoboLat, SjoboLng, 10, bufferKm: 5);
        var fallback = new Zone("Övrigt", isFallback: true);
        var inside = ZoneGeometry.Destination(SjoboLat, SjoboLng, 12, 90);
        var outside = ZoneGeometry.Destination(SjoboLat, SjoboLng, 16, 90);

        ZoneResolver.Resolve(inside.Latitude, inside.Longitude, [sjobo, fallback]).Name.ShouldBe("Sjöbo");
        ZoneResolver.Resolve(outside.Latitude, outside.Longitude, [sjobo, fallback]).IsFallback.ShouldBeTrue();
    }

    [Fact]
    public void Overlap_prefers_the_smaller_zone()
    {
        var wide = CircleAround("Skåne", SjoboLat, SjoboLng, 40);
        var local = CircleAround("Sjöbo", SjoboLat, SjoboLng, 8);
        var fallback = new Zone("Övrigt", isFallback: true);
        ZoneResolver.Resolve(SjoboLat, SjoboLng, [wide, local, fallback]).Name.ShouldBe("Sjöbo");
    }

    [Fact]
    public void Missing_coordinates_fall_back_to_ovrigt()
    {
        var sjobo = CircleAround("Sjöbo", SjoboLat, SjoboLng, 15);
        var fallback = new Zone("Övrigt", isFallback: true);
        ZoneResolver.Resolve(null, null, [sjobo, fallback]).IsFallback.ShouldBeTrue();
    }

    [Fact]
    public void Zone_without_geometry_is_skipped()
    {
        var empty = new Zone("Tom");
        var fallback = new Zone("Övrigt", isFallback: true);
        ZoneResolver.Resolve(SjoboLat, SjoboLng, [empty, fallback]).IsFallback.ShouldBeTrue();
    }

    private static Zone PolygonAround(string name, double lat, double lng, double radiusKm, double bufferKm = 0)
    {
        var zone = new Zone(name);
        zone.SetPolygon(ZoneGeometry.WritePolygon(ZoneGeometry.CirclePolygon(lat, lng, radiusKm)), bufferKm);
        return zone;
    }

    private static Zone CircleAround(string name, double lat, double lng, double radiusKm, double bufferKm = 0)
    {
        var zone = new Zone(name);
        zone.SetCircle(lat, lng, radiusKm, bufferKm);
        return zone;
    }
}
