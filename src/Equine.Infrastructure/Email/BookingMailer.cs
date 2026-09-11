using Equine.Domain.Entities;
using Equine.Infrastructure.Notifications;

namespace Equine.Infrastructure.Email;

public interface IBookingMailer
{
    Task SendVerificationAsync(BookingLine line, string ownerEmail, string ownerName, CancellationToken cancellationToken = default);
    Task SendConfirmationAsync(BookingLine line, string ownerEmail, string ownerName, CancellationToken cancellationToken = default);
    Task SendPractitionerApprovalAsync(BookingLine line, CancellationToken cancellationToken = default);
    Task SendRescheduledAsync(BookingLine line, string ownerEmail, string ownerName, CancellationToken cancellationToken = default);
    Task SendCancelledAsync(BookingLine line, CancellationToken cancellationToken = default);
}

public sealed class BookingMailer : IBookingMailer
{
    private readonly BookingNotifier _notifier;

    public BookingMailer(BookingNotifier notifier) => _notifier = notifier;

    public Task SendVerificationAsync(BookingLine line, string ownerEmail, string ownerName, CancellationToken cancellationToken = default)
        => _notifier.OnWidgetRequestedAsync(line, ownerEmail, ownerName, cancellationToken);

    public Task SendConfirmationAsync(BookingLine line, string ownerEmail, string ownerName, CancellationToken cancellationToken = default)
        => _notifier.OnConfirmedAsync(line, ownerEmail, ownerName, cancellationToken);

    public Task SendPractitionerApprovalAsync(BookingLine line, CancellationToken cancellationToken = default)
        => _notifier.OnPractitionerApprovalNeededAsync(line, cancellationToken);

    public Task SendRescheduledAsync(BookingLine line, string ownerEmail, string ownerName, CancellationToken cancellationToken = default)
        => _notifier.OnRescheduledAsync(line, ownerEmail, ownerName, cancellationToken);

    public Task SendCancelledAsync(BookingLine line, CancellationToken cancellationToken = default)
        => _notifier.OnCancelledAsync(line, cancellationToken);
}
