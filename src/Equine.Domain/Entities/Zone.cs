using System.Text.Json;
using Equine.Domain.Common;
using Equine.Domain.Locations;

namespace Equine.Domain.Entities;

public class Zone : Entity
{
    public string Name { get; private set; } = string.Empty;
    public string PostcodesJson { get; private set; } = "[]";
    public int TravelBufferMinutes { get; private set; }
    public bool IsFallback { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private Zone() { }

    public Zone(string name, IEnumerable<string>? postcodes = null, int travelBufferMinutes = 0, bool isFallback = false)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        TravelBufferMinutes = travelBufferMinutes;
        IsFallback = isFallback;
        SetPostcodes(postcodes ?? []);
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public IReadOnlyList<string> GetPostcodes() =>
        JsonSerializer.Deserialize<List<string>>(PostcodesJson) ?? [];

    public void Update(string? name = null, IEnumerable<string>? postcodes = null, int? travelBufferMinutes = null)
    {
        if (name is not null) Name = name;
        if (postcodes is not null) SetPostcodes(postcodes);
        if (travelBufferMinutes.HasValue) TravelBufferMinutes = travelBufferMinutes.Value;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public bool ContainsPostcode(string? postcode)
    {
        var normalized = AddressNormalization.NormalizePostcode(postcode);
        if (string.IsNullOrEmpty(normalized)) return false;
        return GetPostcodes().Any(p => AddressNormalization.NormalizePostcode(p) == normalized);
    }

    private void SetPostcodes(IEnumerable<string> postcodes)
    {
        var normalized = postcodes
            .Select(AddressNormalization.NormalizePostcode)
            .Where(p => p.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        PostcodesJson = JsonSerializer.Serialize(normalized);
    }
}
