using Equine.Domain.Common;
using System.Text.Json;

namespace Equine.Domain.Entities;

public class WidgetSettings : Entity
{
    public static readonly Guid SingletonId = Guid.Parse("00000000-0000-7000-0000-000000000001");

    public string AllowedOriginsJson { get; private set; } = "[]";
    public bool ShowPrices { get; private set; } = true;
    public string BookingTerms { get; private set; } = string.Empty;
    public string PrivacyPolicyUrl { get; private set; } = string.Empty;
    public int VerificationWindowMinutes { get; private set; } = 30;
    public int CancellationNoticeHours { get; private set; } = 24;
    public string PublicBaseUrl { get; private set; } = "http://localhost:5087";
    public string ContactPhone { get; private set; } = string.Empty;
    public string ContactEmail { get; private set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; private set; } = DateTimeOffset.UtcNow;

    private WidgetSettings() : base(SingletonId)
    {
        AllowedOriginsJson = JsonSerializer.Serialize(new[]
        {
            "http://localhost:4200",
            "http://127.0.0.1:4200",
            "http://localhost:4201",
            "http://127.0.0.1:4201"
        });
    }

    public static WidgetSettings CreateDefault() => new();

    public IReadOnlyList<string> GetAllowedOrigins()
    {
        try
        {
            return JsonSerializer.Deserialize<List<string>>(AllowedOriginsJson) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public void Update(
        IReadOnlyList<string>? allowedOrigins = null,
        bool? showPrices = null,
        string? bookingTerms = null,
        string? privacyPolicyUrl = null,
        int? verificationWindowMinutes = null,
        int? cancellationNoticeHours = null,
        string? publicBaseUrl = null,
        string? contactPhone = null,
        string? contactEmail = null)
    {
        if (allowedOrigins is not null)
            AllowedOriginsJson = JsonSerializer.Serialize(allowedOrigins);
        if (showPrices.HasValue) ShowPrices = showPrices.Value;
        if (bookingTerms is not null) BookingTerms = bookingTerms;
        if (privacyPolicyUrl is not null) PrivacyPolicyUrl = privacyPolicyUrl;
        if (verificationWindowMinutes.HasValue)
            VerificationWindowMinutes = Math.Clamp(verificationWindowMinutes.Value, 5, 24 * 60);
        if (cancellationNoticeHours.HasValue)
            CancellationNoticeHours = Math.Clamp(cancellationNoticeHours.Value, 0, 24 * 14);
        if (publicBaseUrl is not null) PublicBaseUrl = publicBaseUrl.TrimEnd('/');
        if (contactPhone is not null) ContactPhone = contactPhone;
        if (contactEmail is not null) ContactEmail = contactEmail;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
