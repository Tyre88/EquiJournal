using Equine.Domain.Common;

namespace Equine.Domain.Entities;

public class InAppNotification : Entity
{
    public Guid UserId { get; private set; }
    public NotificationType Type { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;
    public string? Link { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ReadAt { get; private set; }

    private InAppNotification() { }

    public InAppNotification(Guid userId, NotificationType type, string title, string body, string? link)
        : base(Guid.CreateVersion7())
    {
        UserId = userId;
        Type = type;
        Title = title;
        Body = body;
        Link = link;
    }

    public void MarkRead() => ReadAt ??= DateTimeOffset.UtcNow;
}
