using Equine.Domain.Entities;
using Equine.Domain.Locations;
using Equine.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Equine.Api.Features.Bookings;

public sealed class LocationResolver
{
    private readonly EquineDbContext _db;

    public LocationResolver(EquineDbContext db) => _db = db;

    public static string KeyFor(Horse horse, Owner owner) =>
        AddressNormalization.LocationKey(
            horse.StableAddress ?? owner.AddressStreet,
            horse.StablePostcode ?? owner.AddressPostcode,
            horse.StableCity ?? owner.AddressCity);

    public static bool SameStable(string visitKey, string horseKey)
    {
        if (AddressNormalization.IsEmptyKey(visitKey) && AddressNormalization.IsEmptyKey(horseKey))
            return true;
        if (AddressNormalization.IsEmptyKey(visitKey) || AddressNormalization.IsEmptyKey(horseKey))
            return false;
        return string.Equals(visitKey, horseKey, StringComparison.Ordinal);
    }

    public async Task<Location> ResolveForHorseAsync(Horse horse, Owner owner, CancellationToken cancellationToken = default)
    {
        var street = horse.StableAddress ?? owner.AddressStreet;
        var postcode = horse.StablePostcode ?? owner.AddressPostcode;
        var city = horse.StableCity ?? owner.AddressCity;
        var lat = horse.StableLatitude ?? owner.Latitude;
        var lng = horse.StableLongitude ?? owner.Longitude;
        var key = AddressNormalization.LocationKey(street, postcode, city);

        if (!AddressNormalization.IsEmptyKey(key))
        {
            var existing = await _db.Locations.FirstOrDefaultAsync(l => l.NormalizedKey == key, cancellationToken);
            if (existing is not null) return existing;
        }

        var name = !string.IsNullOrWhiteSpace(horse.StableLocation)
            ? horse.StableLocation
            : !string.IsNullOrWhiteSpace(city)
                ? $"Stall {city}"
                : $"Stall {owner.Name}";

        var location = new Location(
            LocationType.ClientStable,
            name,
            street,
            postcode,
            city,
            lat,
            lng,
            AddressNormalization.IsEmptyKey(key) ? null : key);

        _db.Locations.Add(location);
        return location;
    }
}
