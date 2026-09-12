using Equine.Api.Features.Bookings;
using Equine.Domain.Entities;
using Equine.Domain.Locations;
using Shouldly;
using Xunit;

namespace Equine.UnitTests.Domain;

public class LocationResolverTests
{
    [Fact]
    public void KeyFor_falls_back_to_owner_address_when_horse_stable_fields_are_empty()
    {
        var owner = new Owner("Ola", "ola@ex.se", addressStreet: "Storgatan 1", addressPostcode: "27531", addressCity: "Sjöbo");
        var horse = new Horse(
            owner.Id,
            "Olsum",
            birthYear: 2018,
            stableAddress: "",
            stablePostcode: "  ",
            stableCity: "");

        var key = LocationResolver.KeyFor(horse, owner);

        key.ShouldBe(AddressNormalization.LocationKey("Storgatan 1", "27531", "Sjöbo"));
    }

    [Fact]
    public void SameStable_is_true_when_location_has_id_key_and_horse_has_no_address()
    {
        var owner = new Owner("Ola", "ola@ex.se");
        var horse = new Horse(owner.Id, "Olsum", birthYear: 2018);
        var location = new Location(LocationType.ClientStable, "Stall Ola");

        location.NormalizedKey.ShouldStartWith("id:");
        LocationResolver.SameStable(LocationResolver.KeyFor(location), LocationResolver.KeyFor(horse, owner)).ShouldBeTrue();
    }

    [Fact]
    public void SameStable_is_false_for_different_addresses()
    {
        var owner = new Owner("Ola", "ola@ex.se", addressStreet: "Storgatan 1", addressPostcode: "27531", addressCity: "Sjöbo");
        var horse = new Horse(
            owner.Id,
            "Olsum",
            birthYear: 2018,
            stableAddress: "Andra vägen 2",
            stablePostcode: "22222",
            stableCity: "Lund");
        var location = new Location(
            LocationType.ClientStable,
            "Stall Sjöbo",
            "Storgatan 1",
            "27531",
            "Sjöbo");

        LocationResolver.SameStable(LocationResolver.KeyFor(location), LocationResolver.KeyFor(horse, owner)).ShouldBeFalse();
    }
}
