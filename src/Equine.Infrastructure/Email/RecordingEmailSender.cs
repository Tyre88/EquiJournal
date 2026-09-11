using System.Collections.Concurrent;

namespace Equine.Infrastructure.Email;

public sealed class RecordingEmailSender : IEmailSender
{
    public ConcurrentBag<EmailMessage> Sent { get; } = new();

    public Task<string?> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        Sent.Add(message);
        return Task.FromResult<string?>($"rec-email-{Guid.CreateVersion7():N}");
    }
}
