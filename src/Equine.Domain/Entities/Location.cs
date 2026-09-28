using Equine.Domain.Common;
using Equine.Domain.Locations;

namespace Equine.Domain.Entities;

public class Location : TenantScopedEntity
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

    public void SetCoordinates(decimal latitude, decimal longitude)
    {
        Latitude = latitude;
        Longitude = longitude;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public string DisplayAddress()
    {
        var parts = new[] { AddressStreet, AddressPostcode, AddressCity }
            .Where(p => !string.IsNullOrWhiteSpace(p));
        return string.Join(", ", parts);
    }
}
