using System.Collections.Concurrent;

namespace Equine.Infrastructure.Sms;

public sealed class RecordingSmsSender : ISmsSender
{
    public ConcurrentBag<SmsMessage> Sent { get; } = new();

    public Task<string?> SendAsync(SmsMessage message, CancellationToken cancellationToken = default)
    {
        Sent.Add(message);
        return Task.FromResult<string?>($"rec-sms-{Guid.CreateVersion7():N}");
    }
}
