using Equine.Domain.Common;
using Equine.Domain.Locations;

namespace Equine.Domain.Entities;

public class PracticeSettings : TenantScopedEntity
{
    public string Name { get; private set; } = "Behandlare";
    public string Clinic { get; private set; } = "HästJournal";
    public string Address { get; private set; } = string.Empty;
    public string AddressStreet { get; private set; } = string.Empty;
    public string AddressPostcode { get; private set; } = string.Empty;
    public string AddressCity { get; private set; } = string.Empty;
    public decimal? Latitude { get; private set; }
    public decimal? Longitude { get; private set; }
    public string Phone { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string VehicleRegistrationNumber { get; private set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; private set; } = DateTimeOffset.UtcNow;

    private PracticeSettings()
    {
    }

    public static PracticeSettings CreateDefault() => new();

    public void Update(
        string name,
        string clinic,
        string address,
        string addressStreet,
        string addressPostcode,
        string addressCity,
        decimal? latitude,
        decimal? longitude,
        string phone,
        string email,
        string? vehicleRegistrationNumber = null)
    {
        Name = name.Trim();
        Clinic = clinic.Trim();
        AddressStreet = addressStreet.Trim();
        AddressPostcode = AddressNormalization.FormatSwedishPostcode(addressPostcode);
        AddressCity = addressCity.Trim();
        Address = string.IsNullOrWhiteSpace(address)
            ? AddressNormalization.FormatDisplay(AddressStreet, AddressPostcode, AddressCity)
            : address.Trim();
        Latitude = latitude;
        Longitude = longitude;
        Phone = phone.Trim();
        Email = email.Trim();
        VehicleRegistrationNumber = NormalizeVehicleRegistration(vehicleRegistrationNumber);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public static string NormalizeVehicleRegistration(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var collapsed = string.Join(" ", value.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return collapsed.Length <= 16 ? collapsed.ToUpperInvariant() : collapsed[..16].Trim().ToUpperInvariant();
    }
}
