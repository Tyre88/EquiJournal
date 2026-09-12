using Equine.Domain.Common;

namespace Equine.Domain.Entities;

public class TreatmentType : TenantScopedEntity
{
    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public string ShortDescription { get; private set; } = string.Empty;
    public string? PublicDescription { get; private set; }
    public int DurationMinutes { get; private set; }
    public int BufferBeforeMinutes { get; private set; }
    public int BufferAfterMinutes { get; private set; }
    public string? Colour { get; private set; }
    public decimal PriceExclVat { get; private set; }
    public decimal VatRate { get; private set; }
    public bool BookableOnline { get; private set; }
    public bool RequiresApproval { get; private set; }
    public int MinNoticeHours { get; private set; }
    public int MaxAdvanceDays { get; private set; }
    public string? AllowedLocationTypes { get; private set; }
    public int? FollowUpIntervalDays { get; private set; }
    public bool ShareSummaryWithClient { get; private set; }
    public TreatmentTypeStatus Status { get; private set; } = TreatmentTypeStatus.Active;
    public string? JournalTemplateJson { get; private set; }
    public int JournalTemplateVersion { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private TreatmentType() { }

    public TreatmentType(
        string name,
        string shortDescription,
        string? publicDescription = null,
        int durationMinutes = 30,
        int bufferBeforeMinutes = 0,
        int bufferAfterMinutes = 0,
        string? colour = null,
        decimal priceExclVat = 0,
        decimal vatRate = 0.25m,
        bool bookableOnline = false,
        bool requiresApproval = false,
        int minNoticeHours = 0,
        int maxAdvanceDays = 30,
        string? allowedLocationTypes = null,
        int? followUpIntervalDays = null,
        string? journalTemplateJson = null,
        string? slug = null)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Slug = string.IsNullOrWhiteSpace(slug) ? TreatmentSlug.FromName(name) : TreatmentSlug.FromName(slug);
        ShortDescription = shortDescription ?? throw new ArgumentNullException(nameof(shortDescription));
        PublicDescription = publicDescription;
        DurationMinutes = durationMinutes;
        BufferBeforeMinutes = bufferBeforeMinutes;
        BufferAfterMinutes = bufferAfterMinutes;
        Colour = colour;
        PriceExclVat = priceExclVat;
        VatRate = vatRate;
        BookableOnline = bookableOnline;
        RequiresApproval = requiresApproval;
        MinNoticeHours = minNoticeHours;
        MaxAdvanceDays = maxAdvanceDays;
        AllowedLocationTypes = allowedLocationTypes;
        FollowUpIntervalDays = followUpIntervalDays;
        ShareSummaryWithClient = false;
        JournalTemplateJson = journalTemplateJson;
        if (journalTemplateJson is not null) JournalTemplateVersion = 1;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Update(
        string? name = null,
        string? shortDescription = null,
        string? publicDescription = null,
        int? durationMinutes = null,
        int? bufferBeforeMinutes = null,
        int? bufferAfterMinutes = null,
        string? colour = null,
        decimal? priceExclVat = null,
        decimal? vatRate = null,
        bool? bookableOnline = null,
        bool? requiresApproval = null,
        int? minNoticeHours = null,
        int? maxAdvanceDays = null,
        string? allowedLocationTypes = null,
        int? followUpIntervalDays = null,
        string? journalTemplateJson = null,
        string? slug = null,
        bool? shareSummaryWithClient = null)
    {
        if (name is not null) Name = name;
        if (!string.IsNullOrWhiteSpace(slug)) Slug = TreatmentSlug.FromName(slug);
        else if (name is not null && string.IsNullOrWhiteSpace(Slug)) Slug = TreatmentSlug.FromName(name);
        if (shortDescription is not null) ShortDescription = shortDescription;
        PublicDescription = publicDescription;
        if (durationMinutes.HasValue) DurationMinutes = durationMinutes.Value;
        if (bufferBeforeMinutes.HasValue) BufferBeforeMinutes = bufferBeforeMinutes.Value;
        if (bufferAfterMinutes.HasValue) BufferAfterMinutes = bufferAfterMinutes.Value;
        Colour = colour;
        if (priceExclVat.HasValue) PriceExclVat = priceExclVat.Value;
        if (vatRate.HasValue) VatRate = vatRate.Value;
        if (bookableOnline.HasValue) BookableOnline = bookableOnline.Value;
        if (requiresApproval.HasValue) RequiresApproval = requiresApproval.Value;
        if (minNoticeHours.HasValue) MinNoticeHours = minNoticeHours.Value;
        if (maxAdvanceDays.HasValue) MaxAdvanceDays = maxAdvanceDays.Value;
        AllowedLocationTypes = allowedLocationTypes;
        FollowUpIntervalDays = followUpIntervalDays;
        if (shareSummaryWithClient.HasValue) ShareSummaryWithClient = shareSummaryWithClient.Value;
        if (journalTemplateJson is not null)
        {
            JournalTemplateJson = journalTemplateJson;
            JournalTemplateVersion++;
        }
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Deactivate()
    {
        Status = TreatmentTypeStatus.Inactive;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Activate()
    {
        Status = TreatmentTypeStatus.Active;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public bool IsActive() => Status == TreatmentTypeStatus.Active;
}
