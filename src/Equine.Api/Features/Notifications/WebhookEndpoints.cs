using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Equine.Infrastructure;
using Equine.Infrastructure.Notifications;
using Equine.Infrastructure.Practice;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Equine.Api.Features.Notifications;

public static class WebhookEndpoints
{
    public static IEndpointRouteBuilder MapWebhookEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/public/webhooks/postmark", async (
            HttpRequest request,
            EquineDbContext db,
            INotificationScheduler scheduler,
            PracticeSettingsService practice,
            IOptions<Equine.Infrastructure.Email.PostmarkOptions> options,
            CancellationToken ct) =>
        {
            var payload = await new StreamReader(request.Body).ReadToEndAsync(ct);
            if (string.IsNullOrWhiteSpace(options.Value.ServerToken)
                || !request.Headers.TryGetValue("X-Postmark-Webhook-Token", out var header)
                || !CryptographicEquals(header.ToString(), options.Value.ServerToken))
                return Results.Unauthorized();

            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(payload) ? "{}" : payload);
            var root = doc.RootElement;
            var messageId = root.TryGetProperty("MessageID", out var mid) ? mid.GetString() : null;
            var recordType = root.TryGetProperty("RecordType", out var rt) ? rt.GetString() : "";
            if (string.IsNullOrWhiteSpace(messageId))
                return Results.Ok();

            var log = await db.NotificationLog.FirstOrDefaultAsync(l => l.ProviderMessageId == messageId, ct);
            if (log is not null)
            {
                log.UpdateDelivery(recordType ?? "Updated", payload[..Math.Min(payload.Length, 500)]);
                if (string.Equals(recordType, "Bounce", StringComparison.OrdinalIgnoreCase)
                    && root.TryGetProperty("Type", out var bounceType)
                    && bounceType.GetString()?.Contains("Hard", StringComparison.OrdinalIgnoreCase) == true)
                {
                    var owner = await db.Owners.FirstOrDefaultAsync(o => o.Email == log.Recipient, ct);
                    if (owner is not null)
                    {
                        owner.FlagEmailInvalid();
                        var p = await practice.GetAsync(ct);
                        if (!string.IsNullOrWhiteSpace(p.Email))
                        {
                            await scheduler.EnqueueAsync(
                                Equine.Domain.Entities.NotificationType.NotificationFailed,
                                Equine.Domain.Entities.NotificationChannel.Email,
                                p.Email,
                                "Owner",
                                owner.Id,
                                DateTimeOffset.UtcNow,
                                new Dictionary<string, string> { ["klientnamn"] = owner.Name },
                                ct);
                        }
                    }
                }
                await db.SaveChangesAsync(ct);
            }

            return Results.Ok();
        }).RequireRateLimiting("public-read").WithName("PostmarkWebhook");

        app.MapPost("/api/public/webhooks/46elks", async (
            HttpRequest request,
            EquineDbContext db,
            IConfiguration config,
            CancellationToken ct) =>
        {
            var secret = config["Elks:WebhookSecret"];
            if (!string.IsNullOrWhiteSpace(secret)
                && request.Query["secret"] != secret)
                return Results.Unauthorized();

            var form = await request.ReadFormAsync(ct);
            var id = form["id"].ToString();
            var status = form["status"].ToString();
            if (string.IsNullOrWhiteSpace(id))
                return Results.Ok();
            var log = await db.NotificationLog.FirstOrDefaultAsync(l => l.ProviderMessageId == id, ct);
            if (log is not null)
            {
                log.UpdateDelivery(string.IsNullOrWhiteSpace(status) ? "Updated" : status, null);
                await db.SaveChangesAsync(ct);
            }
            return Results.Ok();
        }).RequireRateLimiting("public-read").WithName("ElksWebhook");

        return app;
    }

    private static bool CryptographicEquals(string a, string b)
    {
        var ba = Encoding.UTF8.GetBytes(a);
        var bb = Encoding.UTF8.GetBytes(b);
        if (ba.Length != bb.Length) return false;
        return CryptographicOperations.FixedTimeEquals(ba, bb);
    }
}
