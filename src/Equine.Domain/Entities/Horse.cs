using Equine.Domain.Common;

namespace Equine.Domain.Entities;

public class Horse : SoftDeletableEntity
{
    public Guid OwnerId { get; private set; }
    public Owner Owner { get; private set; } = null!;
    public string Name { get; private set; } = string.Empty;
    public Djurslag Species { get; private set; } = Djurslag.Hast;
    public string? Breed { get; private set; }
    public HingstSex Sex { get; private set; } = HingstSex.Okant;
    public int? BirthYear { get; private set; }
    public string? AgeGroup { get; private set; }
    public string? Identity { get; private set; }
    public string? Colour { get; private set; }
    public string? Markings { get; private set; }
    public string? StableLocation { get; private set; }
    public string? StableAddress { get; private set; }
    public string? StablePostcode { get; private set; }
    public string? StableCity { get; private set; }
    public decimal? StableLatitude { get; private set; }
    public decimal? StableLongitude { get; private set; }
    public string? Background { get; private set; }
    public bool EmailVerified { get; private set; } = true;
    public string Status { get; private set; } = "Aktiv";
    public int? FollowUpOverrideDays { get; private set; }
    public DateTimeOffset? FollowUpSnoozedUntil { get; private set; }
    public DateTimeOffset? FollowUpDismissedAt { get; private set; }
    public string? FollowUpDismissedReason { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public ICollection<JournalEntry> JournalEntries { get; private set; } = new List<JournalEntry>();

    private Horse() { }

    public Horse(
        Guid ownerId,
        string name,
        Djurslag species = Djurslag.Hast,
        string? breed = null,
        HingstSex sex = HingstSex.Okant,
        int? birthYear = null,
        string? ageGroup = null,
        string? identity = null,
        string? colour = null,
        string? markings = null,
        string? stableLocation = null,
        string? stableAddress = null,
        string? stablePostcode = null,
        string? stableCity = null,
        decimal? stableLatitude = null,
        decimal? stableLongitude = null,
        string? background = null,
        bool emailVerified = true)
    {
        if (birthYear is null && string.IsNullOrWhiteSpace(ageGroup))
            throw new ArgumentException("Either BirthYear or AgeGroup must be set.");

        OwnerId = ownerId;
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Species = species;
        Breed = breed;
        Sex = sex;
        BirthYear = birthYear;
        AgeGroup = ageGroup;
        Identity = identity;
        Colour = colour;
        Markings = markings;
        StableLocation = stableLocation;
        StableAddress = stableAddress;
        StablePostcode = stablePostcode;
        StableCity = stableCity;
        StableLatitude = stableLatitude;
        StableLongitude = stableLongitude;
        Background = background;
        EmailVerified = emailVerified;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void MarkEmailVerified()
    {
        EmailVerified = true;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Update(
        string? name = null,
        Djurslag? species = null,
        string? breed = null,
        HingstSex? sex = null,
        int? birthYear = null,
        string? ageGroup = null,
        string? identity = null,
        string? colour = null,
        string? markings = null,
        string? stableLocation = null,
        string? stableAddress = null,
        string? stablePostcode = null,
        string? stableCity = null,
        string? background = null,
        string? status = null)
    {
        if (birthYear is null && ageGroup is null && (BirthYear is not null || !string.IsNullOrWhiteSpace(AgeGroup)))
            throw new ArgumentException("Either BirthYear or AgeGroup must be set.");

        if (name is not null) Name = name;
        if (species.HasValue) Species = species.Value;
        Breed = breed;
        if (sex.HasValue) Sex = sex.Value;
        BirthYear = birthYear;
        AgeGroup = ageGroup;
        Identity = identity;
        Colour = colour;
        Markings = markings;
        StableLocation = stableLocation;
        StableAddress = stableAddress;
        StablePostcode = stablePostcode;
        StableCity = stableCity;
        Background = background;
        if (status is not null) Status = status;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SetFollowUpOverride(int? days)
    {
        FollowUpOverrideDays = days is null ? null : Math.Clamp(days.Value, 1, 730);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SnoozeFollowUp(int weeks)
    {
        FollowUpSnoozedUntil = DateTimeOffset.UtcNow.AddDays(7 * Math.Clamp(weeks, 1, 26));
        FollowUpDismissedAt = null;
        FollowUpDismissedReason = null;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void DismissFollowUp(string reason)
    {
        FollowUpDismissedAt = DateTimeOffset.UtcNow;
        FollowUpDismissedReason = reason;
        FollowUpSnoozedUntil = null;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public bool CanArchive() => !JournalEntries.Any(e => e.Status == JournalStatus.Signed);

    public bool HasSignedJournals() => JournalEntries.Any(e => e.Status == JournalStatus.Signed);

    public void Archive()
    {
        Status = "Arkiverad";
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public string ToSearchVector() => $"{Name} {Breed} {Colour} {Markings}";
    public string OwnerSearchVector() => Owner?.Name ?? string.Empty;
}
