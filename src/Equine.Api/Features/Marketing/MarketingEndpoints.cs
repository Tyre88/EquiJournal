using Equine.Domain.Entities;
using Equine.Infrastructure;
using Equine.Infrastructure.Notifications;
using Equine.Infrastructure.Tokens;
using Equine.Infrastructure.Widget;
using Microsoft.EntityFrameworkCore;

namespace Equine.Api.Features.Marketing;

public static class MarketingEndpoints
{
    public static IEndpointRouteBuilder MapMarketingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/app/marketing").RequireAuthorization("CanAdminister");

        group.MapGet("/audience", async (EquineDbContext db, CancellationToken ct) =>
        {
            var count = await db.Owners.CountAsync(o => o.MarketingConsent && !o.EmailInvalid, ct);
            return Results.Ok(new { consented = count });
        }).WithName("MarketingAudience");

        group.MapPost("/send", async (
            MarketingSendRequest request,
            EquineDbContext db,
            INotificationScheduler scheduler,
            WidgetSettingsService widget,
            PublicBookingTokenService tokens,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Subject) || string.IsNullOrWhiteSpace(request.Body))
                return Results.BadRequest(new { error = "Ämne och text krävs." });

            var owners = await db.Owners
                .Where(o => o.MarketingConsent && !o.EmailInvalid)
                .ToListAsync(ct);
            var settings = await widget.GetAsync(ct);
            var queued = 0;
            foreach (var owner in owners)
            {
                var unsub = tokens.Protect(owner.Id, DateTimeOffset.UtcNow.AddYears(1), PublicBookingTokenService.UnsubscribePurpose);
                var unsubUrl = $"{settings.PublicBaseUrl}/api/public/unsubscribe?token={Uri.EscapeDataString(unsub)}";
                await scheduler.EnqueueAsync(
                    NotificationType.MarketingBroadcast,
                    NotificationChannel.Email,
                    owner.Email,
                    nameof(Owner),
                    owner.Id,
                    DateTimeOffset.UtcNow,
                    new Dictionary<string, string>
                    {
                        ["klientnamn"] = owner.Name,
                        ["subject"] = request.Subject,
                        ["behandling"] = request.Body,
                        ["avbokningslänk"] = unsubUrl,
                        ["avbokningslank"] = unsubUrl
                    },
                    ct);
                queued++;
            }

            return Results.Ok(new { queued });
        }).WithName("SendMarketing");

        return app;
    }
}

public sealed record MarketingSendRequest(string Subject, string Body);
