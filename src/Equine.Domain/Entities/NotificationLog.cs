using Equine.Domain.Common;

namespace Equine.Domain.Entities;

public class NotificationLog : TenantScopedEntity
{
    public NotificationType Type { get; private set; }
    public NotificationChannel Channel { get; private set; }
    public string Recipient { get; private set; } = string.Empty;
    public Guid? TemplateId { get; private set; }
    public string? Subject { get; private set; }
    public string? Body { get; private set; }
    public string RelatedEntityType { get; private set; } = string.Empty;
    public Guid RelatedEntityId { get; private set; }
    public string Status { get; private set; } = "Queued";
    public string? ProviderMessageId { get; private set; }
    public string? ProviderResponse { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;

    private NotificationLog() { }

    public NotificationLog(
        NotificationType type,
        NotificationChannel channel,
        string recipient,
        Guid? templateId,
        string? subject,
        string? body,
        string relatedEntityType,
        Guid relatedEntityId,
        string status,
        string? providerMessageId,
        string? providerResponse)
        : base(Guid.CreateVersion7())
    {
        Type = type;
        Channel = channel;
        Recipient = recipient;
        TemplateId = templateId;
        Subject = subject;
        Body = body;
        RelatedEntityType = relatedEntityType;
        RelatedEntityId = relatedEntityId;
        Status = status;
        ProviderMessageId = providerMessageId;
        ProviderResponse = providerResponse;
    }

    public void UpdateDelivery(string status, string? providerResponse)
    {
        Status = status;
        if (providerResponse is not null)
            ProviderResponse = providerResponse;
    }
}
