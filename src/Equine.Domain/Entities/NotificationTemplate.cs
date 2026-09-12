using Equine.Domain.Common;
using Equine.Domain.Notifications;

namespace Equine.Domain.Entities;

public class NotificationTemplate : TenantScopedEntity
{
    public NotificationType Type { get; private set; }
    public NotificationChannel Channel { get; private set; }
    public string Subject { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; private set; } = DateTimeOffset.UtcNow;

    private NotificationTemplate() { }

    public NotificationTemplate(NotificationType type, NotificationChannel channel, string subject, string body)
        : base(Guid.CreateVersion7())
    {
        Type = type;
        Channel = channel;
        Update(subject, body);
    }

    public void Update(string subject, string body)
    {
        var unknown = MergeFields.UnknownFields(subject + " " + body);
        if (unknown.Count > 0)
            throw new ArgumentException("Okända fält: " + string.Join(", ", unknown.Select(f => "{{" + f + "}}")));
        Subject = subject ?? string.Empty;
        Body = body ?? throw new ArgumentNullException(nameof(body));
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
