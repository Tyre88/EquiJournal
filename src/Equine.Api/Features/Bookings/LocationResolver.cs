using Equine.Domain.Entities;
using Equine.Domain.Locations;
using Equine.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Equine.Api.Features.Bookings;

public sealed class LocationResolver
{
    private readonly EquineDbContext _db;

    public LocationResolver(EquineDbContext db) => _db = db;

    public static (string? Street, string? Postcode, string? City, decimal? Lat, decimal? Lng) GetEffectiveStableAddress(
        Horse horse,
        Owner owner) =>
        (
            Coalesce(horse.StableAddress, owner.AddressStreet),
            Coalesce(horse.StablePostcode, owner.AddressPostcode),
            Coalesce(horse.StableCity, owner.AddressCity),
            horse.StableLatitude ?? owner.Latitude,
            horse.StableLongitude ?? owner.Longitude);

    public static string KeyFor(Horse horse, Owner owner)
    {
        var address = GetEffectiveStableAddress(horse, owner);
        return AddressNormalization.LocationKey(address.Street, address.Postcode, address.City);
    }

    public static string KeyFor(Location location) =>
        AddressNormalization.LocationKey(location.AddressStreet, location.AddressPostcode, location.AddressCity);

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
        var (street, postcode, city, lat, lng) = GetEffectiveStableAddress(horse, owner);
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

    private static string? Coalesce(string? horseValue, string? ownerValue) =>
        string.IsNullOrWhiteSpace(horseValue) ? ownerValue : horseValue;
}
