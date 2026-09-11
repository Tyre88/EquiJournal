using Equine.Domain.Common;
using Equine.Domain.Locations;

namespace Equine.Domain.Entities;

public class Location : Entity
{
    public LocationType Type { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? AddressStreet { get; private set; }
    public string? AddressPostcode { get; private set; }
    public string? AddressCity { get; private set; }
    public decimal? Latitude { get; private set; }
    public decimal? Longitude { get; private set; }
    public string NormalizedKey { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private Location() { }

    public Location(
        LocationType type,
        string name,
        string? addressStreet = null,
        string? addressPostcode = null,
        string? addressCity = null,
        decimal? latitude = null,
        decimal? longitude = null,
        string? normalizedKey = null)
    {
        Type = type;
        Name = name ?? throw new ArgumentNullException(nameof(name));
        AddressStreet = addressStreet;
        AddressPostcode = addressPostcode;
        AddressCity = addressCity;
        Latitude = latitude;
        Longitude = longitude;
        NormalizedKey = normalizedKey
            ?? AddressNormalization.LocationKey(addressStreet, addressPostcode, addressCity);
        if (AddressNormalization.IsEmptyKey(NormalizedKey))
            NormalizedKey = $"id:{Id:N}";
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Update(
        string? name = null,
        string? addressStreet = null,
        string? addressPostcode = null,
        string? addressCity = null,
        decimal? latitude = null,
        decimal? longitude = null)
    {
        if (name is not null) Name = name;
        AddressStreet = addressStreet;
        AddressPostcode = addressPostcode;
        AddressCity = addressCity;
        Latitude = latitude;
        Longitude = longitude;
        var key = AddressNormalization.LocationKey(addressStreet, addressPostcode, addressCity);
        NormalizedKey = AddressNormalization.IsEmptyKey(key) ? $"id:{Id:N}" : key;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public string DisplayAddress()
    {
        var parts = new[] { AddressStreet, AddressPostcode, AddressCity }
            .Where(p => !string.IsNullOrWhiteSpace(p));
        return string.Join(", ", parts);
    }
}
