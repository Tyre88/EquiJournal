namespace Equine.Infrastructure.Sms;

public sealed record SmsMessage(string To, string Body);

public interface ISmsSender
{
    Task<string?> SendAsync(SmsMessage message, CancellationToken cancellationToken = default);
}
