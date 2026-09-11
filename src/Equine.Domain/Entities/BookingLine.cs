using Equine.Domain.Common;

namespace Equine.Domain.Entities;

public class BookingLine : Entity
{
    public Guid VisitId { get; private set; }
    public Visit Visit { get; private set; } = null!;
    public Guid HorseId { get; private set; }
    public Horse Horse { get; private set; } = null!;
    public Guid OwnerId { get; private set; }
    public Owner Owner { get; private set; } = null!;
    public Guid TreatmentTypeId { get; private set; }
    public TreatmentType TreatmentType { get; private set; } = null!;
    public string TreatmentName { get; private set; } = string.Empty;
    public int DurationMinutes { get; private set; }
    public decimal Price { get; private set; }
    public decimal VatRate { get; private set; }
    public BookingStatus Status { get; private set; }
    public BookingSource Source { get; private set; }
    public int SortOrder { get; private set; }
    public string? ClientNote { get; private set; }
    public string? InternalNote { get; private set; }
    public string? CancellationReason { get; private set; }
    public DateTimeOffset? CancelledAt { get; private set; }
    public Guid? CancelledBy { get; private set; }
    public Guid? JournalEntryId { get; private set; }
    public DateTimeOffset? EmailVerifiedAt { get; private set; }
    public string? PublicReference { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private BookingLine() { }

    public BookingLine(
        Guid visitId,
        Guid horseId,
        Guid ownerId,
        Guid treatmentTypeId,
        string treatmentName,
        int durationMinutes,
        decimal price,
        decimal vatRate,
        BookingStatus status,
        BookingSource source,
        int sortOrder,
        string? clientNote = null,
        string? internalNote = null,
        string? publicReference = null)
    {
        VisitId = visitId;
        HorseId = horseId;
        OwnerId = ownerId;
        TreatmentTypeId = treatmentTypeId;
        TreatmentName = treatmentName ?? throw new ArgumentNullException(nameof(treatmentName));
        DurationMinutes = durationMinutes;
        Price = price;
        VatRate = vatRate;
        Status = status;
        Source = source;
        SortOrder = sortOrder;
        ClientNote = clientNote;
        InternalNote = internalNote;
        PublicReference = publicReference;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void MarkEmailVerified()
    {
        EmailVerifiedAt = DateTimeOffset.UtcNow;
        Touch();
    }

    public bool IsEmailVerified => EmailVerifiedAt is not null;

    public bool BlocksSlot =>
        Status is BookingStatus.Requested or BookingStatus.Confirmed;

    public void Approve()
    {
        EnsureStatus(BookingStatus.Requested, nameof(Approve));
        Status = BookingStatus.Confirmed;
        Touch();
    }

    public void Cancel(string? reason, Guid? cancelledBy)
    {
        if (Status is not (BookingStatus.Requested or BookingStatus.Confirmed))
            throw new InvalidOperationException($"Cannot cancel a booking in status {Status}.");

        Status = BookingStatus.Cancelled;
        CancellationReason = reason;
        CancelledAt = DateTimeOffset.UtcNow;
        CancelledBy = cancelledBy;
        Touch();
    }

    public void Complete(Guid journalEntryId)
    {
        EnsureStatus(BookingStatus.Confirmed, nameof(Complete));
        Status = BookingStatus.Completed;
        JournalEntryId = journalEntryId;
        Touch();
    }

    public void MarkNoShow()
    {
        EnsureStatus(BookingStatus.Confirmed, nameof(MarkNoShow));
        Status = BookingStatus.NoShow;
        Touch();
    }

    public void UpdateNotes(string? clientNote = null, string? internalNote = null, bool updateClient = false, bool updateInternal = false)
    {
        if (updateClient) ClientNote = clientNote;
        if (updateInternal) InternalNote = internalNote;
        Touch();
    }

    private void EnsureStatus(BookingStatus expected, string action)
    {
        if (Status != expected)
            throw new InvalidOperationException($"Cannot {action} a booking in status {Status}.");
    }

    private void Touch() => UpdatedAt = DateTimeOffset.UtcNow;
}
