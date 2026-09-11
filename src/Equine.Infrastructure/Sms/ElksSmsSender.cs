using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Equine.Infrastructure.Sms;

public sealed class ElksSmsSender : ISmsSender
{
    private readonly HttpClient _http;
    private readonly ElksOptions _options;

    public ElksSmsSender(HttpClient http, IOptions<ElksOptions> options)
    {
        _http = http;
        _options = options.Value;
    }

    public async Task<string?> SendAsync(SmsMessage message, CancellationToken cancellationToken = default)
    {
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["from"] = _options.From,
            ["to"] = message.To,
            ["message"] = message.Body
        });
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_options.Username}:{_options.Password}"));
        using var request = new HttpRequestMessage(HttpMethod.Post, _options.ApiUrl) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        var response = await _http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        response.EnsureSuccessStatusCode();
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("id", out var id))
                return id.GetString();
        }
        catch (JsonException)
        {
        }
        return null;
    }
}
