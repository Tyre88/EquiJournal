using Equine.Domain.Common;

namespace Equine.Domain.Entities;

public class Owner : SoftDeletableEntity
{
    public string Name { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string? Phone { get; private set; }
    public string? AddressStreet { get; private set; }
    public string? AddressPostcode { get; private set; }
    public string? AddressCity { get; private set; }
    public decimal? Latitude { get; private set; }
    public decimal? Longitude { get; private set; }
    public string? Notes { get; private set; }
    public bool MarketingConsent { get; private set; }
    public DateTimeOffset? MarketingConsentAt { get; private set; }
    public bool EmailVerified { get; private set; } = true;
    public bool EmailInvalid { get; private set; }
    public Guid? UserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public ICollection<Horse> Horses { get; private set; } = new List<Horse>();

    private Owner() { }

    public Owner(
        string name,
        string email,
        string? phone = null,
        string? addressStreet = null,
        string? addressPostcode = null,
        string? addressCity = null,
        decimal? latitude = null,
        decimal? longitude = null,
        string? notes = null,
        bool marketingConsent = false,
        bool emailVerified = true)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Email = email ?? throw new ArgumentNullException(nameof(email));
        Phone = phone;
        AddressStreet = addressStreet;
        AddressPostcode = addressPostcode;
        AddressCity = addressCity;
        Latitude = latitude;
        Longitude = longitude;
        Notes = notes;
        MarketingConsent = marketingConsent;
        MarketingConsentAt = marketingConsent ? DateTimeOffset.UtcNow : null;
        EmailVerified = emailVerified;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void MarkEmailVerified()
    {
        EmailVerified = true;
        EmailInvalid = false;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void FlagEmailInvalid()
    {
        EmailInvalid = true;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void LinkUser(Guid userId)
    {
        UserId = userId;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SetMarketingConsent(bool consent)
    {
        MarketingConsent = consent;
        MarketingConsentAt = consent ? DateTimeOffset.UtcNow : null;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Update(
        string? name = null,
        string? email = null,
        string? phone = null,
        string? addressStreet = null,
        string? addressPostcode = null,
        string? addressCity = null,
        string? notes = null,
        bool? marketingConsent = null,
        decimal? latitude = null,
        decimal? longitude = null)
    {
        if (name is not null) Name = name;
        if (email is not null) Email = email;
        Phone = phone;
        AddressStreet = addressStreet;
        AddressPostcode = addressPostcode;
        AddressCity = addressCity;
        Notes = notes;
        Latitude = latitude;
        Longitude = longitude;
        if (marketingConsent.HasValue && marketingConsent.Value != MarketingConsent)
        {
            MarketingConsent = marketingConsent.Value;
            MarketingConsentAt = marketingConsent.Value ? DateTimeOffset.UtcNow : null;
        }
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public string ToSearchVector() => $"{Name} {Email} {Phone} {AddressCity}";
}
