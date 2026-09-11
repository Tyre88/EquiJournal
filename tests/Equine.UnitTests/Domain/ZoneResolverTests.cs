using Equine.Domain.Entities;
using Equine.Domain.Locations;
using Shouldly;
using Xunit;

namespace Equine.UnitTests.Domain;

public class ZoneResolverTests
{
    [Fact]
    public void Matching_postcode_resolves_named_zone()
    {
        var sjobo = new Zone("Sjöbo/Tomelilla", ["27531", "273 50"]);
        var fallback = new Zone("Övrigt", isFallback: true);
        ZoneResolver.Resolve("27350", [sjobo, fallback]).Name.ShouldBe("Sjöbo/Tomelilla");
    }

    [Fact]
    public void Unknown_postcode_falls_back_to_ovrigt()
    {
        var sjobo = new Zone("Sjöbo/Tomelilla", ["27531"]);
        var fallback = new Zone("Övrigt", isFallback: true);
        ZoneResolver.Resolve("22100", [sjobo, fallback]).IsFallback.ShouldBeTrue();
    }
}
