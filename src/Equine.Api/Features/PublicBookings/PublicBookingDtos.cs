namespace Equine.Api.Features.PublicBookings;

public sealed record PublicCreateBookingRequest(
    Guid TreatmentId,
    DateTimeOffset StartsAt,
    PublicHorseDetails Horse,
    PublicOwnerDetails Owner,
    bool Consent,
    string? Note = null,
    string? Website = null,
    DateTimeOffset? FormOpenedAt = null);

public sealed record PublicHorseDetails(
    string Name,
    int? BirthYear = null,
    string? AgeGroup = null,
    string? Breed = null,
    string? KnownIssues = null);

public sealed record PublicOwnerDetails(
    string Name,
    string Email,
    string Phone,
    string AddressStreet,
    string AddressPostcode,
    string? AddressCity = null);

public sealed record PublicVerifyRequest(string Token);
public sealed record PublicRescheduleRequest(DateTimeOffset StartsAt);
public sealed record PublicCancelRequest(string? Reason = null);
