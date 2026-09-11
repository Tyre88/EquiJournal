using System.Collections.Concurrent;

namespace Equine.Infrastructure.Notifications;

public sealed class RecordingWebPushSender : IWebPushSender
{
    public ConcurrentBag<(Guid UserId, string Title, string Body)> Sent { get; } = new();

    public Task SendAsync(Guid userId, string title, string body, CancellationToken cancellationToken = default)
    {
        Sent.Add((userId, title, body));
        return Task.CompletedTask;
    }
}
