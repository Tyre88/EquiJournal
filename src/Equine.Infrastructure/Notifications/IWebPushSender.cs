namespace Equine.Infrastructure.Notifications;

public interface IWebPushSender
{
    Task SendAsync(Guid userId, string title, string body, CancellationToken cancellationToken = default);
}
