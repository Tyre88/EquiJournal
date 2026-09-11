using System.Security.Cryptography;
using System.Text.Json;

namespace Equine.Domain.Entities;

public class JournalEntry
{
    public Guid Id { get; private set; }
    public Guid HorseId { get; private set; }
    public Horse Horse { get; private set; } = null!;
    public string OwnerSnapshot { get; private set; } = string.Empty;
    public string Species { get; private set; } = string.Empty;
    public string? Sex { get; private set; }
    public string? AgeOrAgeGroup { get; private set; }
    public string? AnimalIdentity { get; private set; }
    public DateTimeOffset PerformedAt { get; private set; }
    public string Anamnes { get; private set; } = string.Empty;
    public string StatusKlinisk { get; private set; } = string.Empty;
    public string Atgarder { get; private set; } = string.Empty;
    public string? Diagnos { get; private set; }
    public string? Differentialdiagnoser { get; private set; }
    public string? PrognosOchPlan { get; private set; }
    public Guid? TreatmentTypeId { get; private set; }
    public string? TreatmentTypeName { get; private set; }
    public int? DurationMinutes { get; private set; }
    public decimal? Price { get; private set; }
    public string TemplateDataJson { get; private set; } = "{}";
    public int TemplateVersion { get; private set; }
    public JournalStatus Status { get; private set; } = JournalStatus.Draft;
    public DateTimeOffset? SignedAt { get; private set; }
    public string? SignedBy { get; private set; }
    public string? ContentHash { get; private set; }
    public Guid? BookingLineId { get; private set; }
    public JournalSource Source { get; private set; } = JournalSource.Practice;
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public ICollection<JournalAmendment> Amendments { get; private set; } = new List<JournalAmendment>();
    public ICollection<Attachment> Attachments { get; private set; } = new List<Attachment>();

    private JournalEntry() { }

    public JournalEntry(
        Guid horseId,
        string ownerSnapshot,
        string species,
        string? sex,
        string? ageOrAgeGroup,
        string? animalIdentity,
        DateTimeOffset performedAt,
        string anamnes,
        string statusKlinisk,
        string atgarder,
        string? diagnos = null,
        string? differentialdiagnoser = null,
        string? prognosOchPlan = null,
        Guid? treatmentTypeId = null,
        string? treatmentTypeName = null,
        int? durationMinutes = null,
        decimal? price = null,
        string? templateDataJson = null,
        int? templateVersion = null,
        Guid createdBy = default,
        Guid? bookingLineId = null,
        JournalSource source = JournalSource.Practice)
    {
        HorseId = horseId;
        OwnerSnapshot = ownerSnapshot ?? throw new ArgumentNullException(nameof(ownerSnapshot));
        Species = species ?? throw new ArgumentNullException(nameof(species));
        Sex = sex;
        AgeOrAgeGroup = ageOrAgeGroup;
        AnimalIdentity = animalIdentity;
        PerformedAt = performedAt;
        Anamnes = anamnes ?? string.Empty;
        StatusKlinisk = statusKlinisk ?? string.Empty;
        Atgarder = atgarder ?? string.Empty;
        Diagnos = diagnos;
        Differentialdiagnoser = differentialdiagnoser;
        PrognosOchPlan = prognosOchPlan;
        TreatmentTypeId = treatmentTypeId;
        TreatmentTypeName = treatmentTypeName;
        DurationMinutes = durationMinutes;
        Price = price;
        TemplateDataJson = templateDataJson ?? "{}";
        TemplateVersion = templateVersion ?? 1;
        CreatedBy = createdBy == default ? Guid.CreateVersion7() : createdBy;
        BookingLineId = bookingLineId;
        Source = source;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void UpdateDraft(
        string anamnes,
        string statusKlinisk,
        string atgarder,
        string? diagnos = null,
        string? differentialdiagnoser = null,
        string? prognosOchPlan = null,
        string? templateDataJson = null,
        int? durationMinutes = null,
        decimal? price = null)
    {
        if (Status == JournalStatus.Signed)
            throw new InvalidOperationException("Cannot update a signed journal entry.");

        Anamnes = anamnes ?? string.Empty;
        StatusKlinisk = statusKlinisk ?? string.Empty;
        Atgarder = atgarder ?? string.Empty;
        Diagnos = diagnos;
        Differentialdiagnoser = differentialdiagnoser;
        PrognosOchPlan = prognosOchPlan;
        if (templateDataJson is not null) TemplateDataJson = templateDataJson;
        if (durationMinutes.HasValue) DurationMinutes = durationMinutes.Value;
        if (price.HasValue) Price = price.Value;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public bool ValidateForSigning()
    {
        if (string.IsNullOrWhiteSpace(Anamnes)) return false;
        if (string.IsNullOrWhiteSpace(StatusKlinisk)) return false;
        if (string.IsNullOrWhiteSpace(Atgarder)) return false;
        return true;
    }

    public void Sign(string signedBy)
    {
        if (!ValidateForSigning())
            throw new InvalidOperationException("Cannot sign journal: required fields are missing.");

        Status = JournalStatus.Signed;
        SignedAt = DateTimeOffset.UtcNow;
        SignedBy = signedBy;
        ContentHash = ComputeContentHash();
    }

    public void SignImported(string signedBy, DateTimeOffset performedAt)
    {
        if (Status == JournalStatus.Signed)
            throw new InvalidOperationException("Already signed.");

        Source = JournalSource.Import;
        PerformedAt = performedAt;
        Status = JournalStatus.Signed;
        SignedAt = performedAt;
        SignedBy = signedBy;
        ContentHash = ComputeContentHash();
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public string ComputeContentHash()
    {
        var content = JsonSerializer.Serialize(new
        {
            OwnerSnapshot,
            Species,
            Sex,
            AgeOrAgeGroup,
            AnimalIdentity,
            PerformedAt,
            Anamnes,
            StatusKlinisk,
            Atgarder,
            Diagnos,
            Differentialdiagnoser,
            PrognosOchPlan,
            TreatmentTypeName,
            DurationMinutes,
            Price,
            TemplateDataJson,
            TemplateVersion
        }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = false });

        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(content));
        return Convert.ToHexStringLower(bytes);
    }
}
