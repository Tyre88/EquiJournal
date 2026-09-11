using Equine.Api.Auth;
using Equine.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Equine.Api.Features.Notifications;

public static class InAppNotificationEndpoints
{
    public static IEndpointRouteBuilder MapInAppNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/app/notifications").RequireAuthorization();

        group.MapGet("/", async (HttpContext http, EquineDbContext db, CancellationToken ct) =>
        {
            var userId = CurrentUser.GetUserId(http);
            if (userId is null) return Results.Unauthorized();
            var items = await db.InAppNotifications
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .Take(50)
                .Select(n => new { n.Id, type = n.Type.ToString(), n.Title, n.Body, n.Link, n.CreatedAt, n.ReadAt })
                .ToListAsync(ct);
            var unread = items.Count(n => n.ReadAt is null);
            return Results.Ok(new { unread, items });
        }).WithName("ListInAppNotifications");

        group.MapPost("/{id:guid}/read", async (Guid id, HttpContext http, EquineDbContext db, CancellationToken ct) =>
        {
            var userId = CurrentUser.GetUserId(http);
            var row = await db.InAppNotifications.FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId, ct);
            if (row is null) return Results.NotFound();
            row.MarkRead();
            await db.SaveChangesAsync(ct);
            return Results.Ok();
        }).WithName("ReadInAppNotification");

        group.MapPost("/read-all", async (HttpContext http, EquineDbContext db, CancellationToken ct) =>
        {
            var userId = CurrentUser.GetUserId(http);
            var rows = await db.InAppNotifications.Where(n => n.UserId == userId && n.ReadAt == null).ToListAsync(ct);
            foreach (var row in rows) row.MarkRead();
            await db.SaveChangesAsync(ct);
            return Results.Ok();
        }).WithName("ReadAllInAppNotifications");

        group.MapPost("/push-subscription", async (
            PushSubscribeRequest request,
            HttpContext http,
            EquineDbContext db,
            CancellationToken ct) =>
        {
            var userId = CurrentUser.GetUserId(http);
            if (userId is null) return Results.Unauthorized();
            if (string.IsNullOrWhiteSpace(request.Endpoint)) return Results.BadRequest();
            var existing = await db.WebPushEndpoints.FirstOrDefaultAsync(e => e.Endpoint == request.Endpoint, ct);
            if (existing is null)
            {
                db.WebPushEndpoints.Add(new Equine.Domain.Entities.WebPushEndpoint(
                    userId.Value, request.Endpoint, request.P256dh ?? "", request.Auth ?? ""));
                await db.SaveChangesAsync(ct);
            }
            return Results.Ok();
        }).WithName("SubscribeWebPush");

        return app;
    }
}

public sealed record PushSubscribeRequest(string Endpoint, string? P256dh, string? Auth);
