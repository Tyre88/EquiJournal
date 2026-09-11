using Equine.Domain.Common;

namespace Equine.Domain.Entities;

public class ScheduledNotification : Entity
{
    public NotificationType Type { get; private set; }
    public NotificationChannel Channel { get; private set; }
    public string Recipient { get; private set; } = string.Empty;
    public string RelatedEntityType { get; private set; } = string.Empty;
    public Guid RelatedEntityId { get; private set; }
    public DateTimeOffset ScheduledFor { get; private set; }
    public string PayloadJson { get; private set; } = "{}";
    public NotificationDeliveryStatus Status { get; private set; } = NotificationDeliveryStatus.Pending;
    public int Attempts { get; private set; }
    public string? LastError { get; private set; }
    public DateTimeOffset? SentAt { get; private set; }
    public string? ProviderMessageId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;

    private ScheduledNotification() { }

    public ScheduledNotification(
        NotificationType type,
        NotificationChannel channel,
        string recipient,
        string relatedEntityType,
        Guid relatedEntityId,
        DateTimeOffset scheduledFor,
        string payloadJson)
        : base(Guid.CreateVersion7())
    {
        Type = type;
        Channel = channel;
        Recipient = recipient;
        RelatedEntityType = relatedEntityType;
        RelatedEntityId = relatedEntityId;
        ScheduledFor = scheduledFor;
        PayloadJson = payloadJson;
    }

    public void Reschedule(DateTimeOffset when) => ScheduledFor = when;

    public void MarkSending() => Status = NotificationDeliveryStatus.Sending;

    public void MarkAttempt(string? error)
    {
        Attempts++;
        LastError = error;
        if (Attempts >= 5)
        {
            Status = NotificationDeliveryStatus.Failed;
            return;
        }

        Status = NotificationDeliveryStatus.Pending;
        ScheduledFor = DateTimeOffset.UtcNow.AddMinutes(Math.Pow(2, Attempts));
    }

    public void MarkSent(string? providerMessageId)
    {
        Status = NotificationDeliveryStatus.Sent;
        SentAt = DateTimeOffset.UtcNow;
        ProviderMessageId = providerMessageId;
        LastError = null;
    }

    public void Cancel()
    {
        if (Status == NotificationDeliveryStatus.Pending)
            Status = NotificationDeliveryStatus.Cancelled;
    }
}
