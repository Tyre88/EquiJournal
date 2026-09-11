using Equine.Domain.Entities;
using Equine.Infrastructure;
using Equine.Infrastructure.Notifications;
using Equine.Infrastructure.Practice;
using Equine.Infrastructure.Widget;
using Microsoft.EntityFrameworkCore;

namespace Equine.Api.Features.FollowUps;

public static class FollowUpEndpoints
{
    public static IEndpointRouteBuilder MapFollowUpEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/app/follow-ups").RequireAuthorization("CanManageBookings");

        group.MapGet("/", async (EquineDbContext db, CancellationToken ct) =>
        {
            var now = DateTimeOffset.UtcNow;
            var horses = await db.Horses
                .Include(h => h.Owner)
                .Include(h => h.JournalEntries)
                .Where(h => h.FollowUpDismissedAt == null)
                .ToListAsync(ct);

            var treatments = await db.TreatmentTypes.ToDictionaryAsync(t => t.Id, ct);
            var items = new List<object>();
            foreach (var horse in horses)
            {
                if (horse.FollowUpSnoozedUntil is DateTimeOffset snooze && snooze > now)
                    continue;
                var last = horse.JournalEntries
                    .Where(j => j.Status == JournalStatus.Signed)
                    .OrderByDescending(j => j.PerformedAt)
                    .FirstOrDefault();
                if (last is null) continue;
                var interval = horse.FollowUpOverrideDays
                               ?? (last.TreatmentTypeId is Guid tid && treatments.TryGetValue(tid, out var t)
                                   ? t.FollowUpIntervalDays
                                   : null);
                if (interval is null) continue;
                var due = last.PerformedAt.AddDays(interval.Value);
                if (due > now) continue;
                items.Add(new
                {
                    horseId = horse.Id,
                    horseName = horse.Name,
                    ownerName = horse.Owner.Name,
                    ownerEmail = horse.Owner.Email,
                    lastTreatment = last.TreatmentTypeName,
                    lastPerformedAt = last.PerformedAt,
                    dueAt = due,
                    daysOverdue = (int)(now - due).TotalDays,
                    horse.FollowUpOverrideDays
                });
            }

            return Results.Ok(items.OrderByDescending(i => ((dynamic)i).daysOverdue));
        }).WithName("ListFollowUps");

        group.MapPost("/{horseId:guid}/remind", async (
            Guid horseId,
            EquineDbContext db,
            INotificationScheduler scheduler,
            PracticeSettingsService practice,
            WidgetSettingsService widget,
            CancellationToken ct) =>
        {
            var horse = await db.Horses
                .Include(h => h.Owner)
                .Include(h => h.JournalEntries)
                .FirstOrDefaultAsync(h => h.Id == horseId, ct);
            if (horse is null) return Results.NotFound();
            if (string.IsNullOrWhiteSpace(horse.Owner.Email) || horse.Owner.EmailInvalid)
                return Results.BadRequest(new { error = "Kunden saknar giltig e-post." });
            var p = await practice.GetAsync(ct);
            var w = await widget.GetAsync(ct);
            var payload = await FollowUpNotificationHelper.BuildPayloadAsync(
                horse, p.Clinic, w.PublicBaseUrl, db, ct);
            await scheduler.EnqueueAsync(
                NotificationType.FollowUpDue,
                NotificationChannel.Email,
                horse.Owner.Email,
                nameof(Horse),
                horse.Id,
                DateTimeOffset.UtcNow,
                payload,
                ct);
            return Results.Ok();
        }).WithName("RemindFollowUp");

        group.MapPost("/{horseId:guid}/snooze", async (Guid horseId, SnoozeRequest request, EquineDbContext db, CancellationToken ct) =>
        {
            var horse = await db.Horses.FirstOrDefaultAsync(h => h.Id == horseId, ct);
            if (horse is null) return Results.NotFound();
            horse.SnoozeFollowUp(request.Weeks);
            await db.SaveChangesAsync(ct);
            return Results.Ok();
        }).WithName("SnoozeFollowUp");

        group.MapPost("/{horseId:guid}/dismiss", async (Guid horseId, DismissRequest request, EquineDbContext db, CancellationToken ct) =>
        {
            var horse = await db.Horses.FirstOrDefaultAsync(h => h.Id == horseId, ct);
            if (horse is null) return Results.NotFound();
            if (string.IsNullOrWhiteSpace(request.Reason))
                return Results.BadRequest(new { error = "Ange en anledning." });
            horse.DismissFollowUp(request.Reason);
            await db.SaveChangesAsync(ct);
            return Results.Ok();
        }).WithName("DismissFollowUp");

        return app;
    }
}

public sealed record SnoozeRequest(int Weeks = 2);
public sealed record DismissRequest(string Reason);
