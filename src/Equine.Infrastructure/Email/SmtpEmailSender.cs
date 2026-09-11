using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Equine.Infrastructure.Email;

public sealed class SmtpEmailSender : IEmailSender
{
    private readonly SmtpOptions _options;

    public SmtpEmailSender(IOptions<SmtpOptions> options) => _options = options.Value;

    public async Task<string?> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(_options.FromName, _options.From));
        mime.To.Add(MailboxAddress.Parse(message.To));
        mime.Subject = message.Subject;
        if (!string.IsNullOrWhiteSpace(message.ReplyTo))
            mime.ReplyTo.Add(MailboxAddress.Parse(message.ReplyTo));
        if (!string.IsNullOrWhiteSpace(message.ListUnsubscribe))
            mime.Headers.Add("List-Unsubscribe", message.ListUnsubscribe);

        var builder = new BodyBuilder
        {
            TextBody = message.TextBody,
            HtmlBody = message.HtmlBody
        };
        if (message.Attachments is not null)
        {
            foreach (var attachment in message.Attachments)
                builder.Attachments.Add(attachment.FileName, attachment.Content, ContentType.Parse(attachment.ContentType));
        }
        mime.Body = builder.ToMessageBody();

        using var client = new SmtpClient
        {
            Timeout = 10_000
        };
        var socket = _options.UseSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto;
        await client.ConnectAsync(_options.Host, _options.Port, socket, cancellationToken);
        if (!string.IsNullOrWhiteSpace(_options.UserName))
            await client.AuthenticateAsync(_options.UserName, _options.Password, cancellationToken);
        await client.SendAsync(mime, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
        return mime.MessageId;
    }
}
