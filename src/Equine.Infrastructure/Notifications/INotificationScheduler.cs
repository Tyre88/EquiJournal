using Equine.Domain.Entities;

namespace Equine.Infrastructure.Notifications;

public interface INotificationScheduler
{
    Task EnqueueAsync(
        NotificationType type,
        NotificationChannel channel,
        string recipient,
        string relatedEntityType,
        Guid relatedEntityId,
        DateTimeOffset scheduledFor,
        IReadOnlyDictionary<string, string> payload,
        CancellationToken cancellationToken = default);

    Task CancelPendingAsync(NotificationType type, Guid relatedEntityId, CancellationToken cancellationToken = default);

    Task CancelPendingAsync(Guid relatedEntityId, CancellationToken cancellationToken = default);
}
