namespace Equine.Infrastructure.Email;

public sealed record EmailAttachment(string FileName, string ContentType, byte[] Content);

public sealed record EmailMessage(
    string To,
    string Subject,
    string TextBody,
    string? HtmlBody = null,
    IReadOnlyList<EmailAttachment>? Attachments = null,
    string? ReplyTo = null,
    string? ListUnsubscribe = null);

public interface IEmailSender
{
    Task<string?> SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
