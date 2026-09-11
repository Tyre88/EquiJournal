using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Equine.Infrastructure.Email;

public sealed class PostmarkEmailSender : IEmailSender
{
    private readonly HttpClient _http;
    private readonly PostmarkOptions _options;

    public PostmarkEmailSender(HttpClient http, IOptions<PostmarkOptions> options)
    {
        _http = http;
        _options = options.Value;
    }

    public async Task<string?> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        var payload = new Dictionary<string, object?>
        {
            ["From"] = $"{_options.FromName} <{_options.From}>",
            ["To"] = message.To,
            ["Subject"] = message.Subject,
            ["TextBody"] = message.TextBody,
            ["HtmlBody"] = message.HtmlBody ?? $"<pre>{System.Net.WebUtility.HtmlEncode(message.TextBody)}</pre>",
            ["MessageStream"] = _options.MessageStream,
            ["ReplyTo"] = message.ReplyTo ?? _options.ReplyTo
        };
        if (!string.IsNullOrWhiteSpace(message.ListUnsubscribe))
        {
            payload["Headers"] = new[]
            {
                new { Name = "List-Unsubscribe", Value = message.ListUnsubscribe },
                new { Name = "List-Unsubscribe-Post", Value = "List-Unsubscribe=One-Click" }
            };
        }
        if (message.Attachments is { Count: > 0 })
        {
            payload["Attachments"] = message.Attachments.Select(a => new
            {
                Name = a.FileName,
                Content = Convert.ToBase64String(a.Content),
                ContentType = a.ContentType
            }).ToArray();
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.postmarkapp.com/email")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Add("X-Postmark-Server-Token", _options.ServerToken);
        var response = await _http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.TryGetProperty("MessageID", out var id) ? id.GetString() : null;
    }
}
