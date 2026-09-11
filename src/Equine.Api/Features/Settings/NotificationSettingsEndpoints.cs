using Equine.Domain.Entities;
using Equine.Domain.Notifications;
using Equine.Infrastructure;
using Equine.Infrastructure.Notifications;
using Microsoft.EntityFrameworkCore;

namespace Equine.Api.Features.Settings;

public static class NotificationSettingsEndpoints
{
    public static IEndpointRouteBuilder MapNotificationSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/app/notification-settings").RequireAuthorization("CanAdminister");

        group.MapGet("/", async (NotificationSettingsService settings, EquineDbContext db, CancellationToken ct) =>
        {
            var s = await settings.GetAsync(ct);
            var templates = await db.NotificationTemplates.OrderBy(t => t.Type).ThenBy(t => t.Channel).ToListAsync(ct);
            return Results.Ok(new
            {
                enabled = s.GetEnabled(),
                s.ReminderLeadHours,
                quietHoursStart = s.QuietHoursStart.ToString("HH:mm"),
                quietHoursEnd = s.QuietHoursEnd.ToString("HH:mm"),
                s.DailySmsCap,
                morningSummaryTime = s.MorningSummaryTime.ToString("HH:mm"),
                mergeFields = MergeFields.Allowed,
                templates = templates.Select(t => new
                {
                    t.Id,
                    type = t.Type.ToString(),
                    channel = t.Channel.ToString(),
                    t.Subject,
                    t.Body
                })
            });
        }).WithName("GetNotificationSettings");

        group.MapPut("/", async (NotificationSettingsUpdateRequest request, NotificationSettingsService settings, CancellationToken ct) =>
        {
            TimeOnly? start = TimeOnly.TryParse(request.QuietHoursStart, out var qs) ? qs : null;
            TimeOnly? end = TimeOnly.TryParse(request.QuietHoursEnd, out var qe) ? qe : null;
            TimeOnly? morning = TimeOnly.TryParse(request.MorningSummaryTime, out var ms) ? ms : null;
            var s = await settings.UpdateAsync(request.Enabled, request.ReminderLeadHours, start, end, request.DailySmsCap, morning, ct);
            return Results.Ok(new
            {
                enabled = s.GetEnabled(),
                s.ReminderLeadHours,
                quietHoursStart = s.QuietHoursStart.ToString("HH:mm"),
                quietHoursEnd = s.QuietHoursEnd.ToString("HH:mm"),
                s.DailySmsCap,
                morningSummaryTime = s.MorningSummaryTime.ToString("HH:mm")
            });
        }).WithName("UpdateNotificationSettings");

        group.MapPut("/templates/{id:guid}", async (
            Guid id,
            TemplateUpdateRequest request,
            EquineDbContext db,
            CancellationToken ct) =>
        {
            var template = await db.NotificationTemplates.FirstOrDefaultAsync(t => t.Id == id, ct);
            if (template is null) return Results.NotFound();
            try
            {
                template.Update(request.Subject ?? "", request.Body ?? "");
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { template.Id, template.Subject, template.Body });
        }).WithName("UpdateNotificationTemplate");

        group.MapPost("/templates/preview", (TemplatePreviewRequest request) =>
        {
            var unknown = MergeFields.UnknownFields((request.Subject ?? "") + " " + (request.Body ?? ""));
            if (unknown.Count > 0)
                return Results.BadRequest(new { error = "Okända fält: " + string.Join(", ", unknown.Select(f => "{{" + f + "}}")) });
            var sample = new Dictionary<string, string>
            {
                ["klientnamn"] = "Anna Andersson",
                ["hästnamn"] = "Saga",
                ["hastnamn"] = "Saga",
                ["behandling"] = "Massage",
                ["datum"] = "2026-09-12",
                ["tid"] = "10:00",
                ["adress"] = "Stallet 1, 275 31 Sjöbo",
                ["avbokningslänk"] = "https://example.test/avboka",
                ["avbokningslank"] = "https://example.test/avboka",
                ["kliniknamn"] = "HästJournal",
                ["telefon"] = "070-000 00 00",
                ["referens"] = "HJ-1234"
            };
            return Results.Ok(new
            {
                subject = MergeFields.Render(request.Subject ?? "", sample),
                body = MergeFields.Render(request.Body ?? "", sample)
            });
        }).WithName("PreviewNotificationTemplate");

        return app;
    }
}

public sealed record NotificationSettingsUpdateRequest(
    Dictionary<string, Dictionary<string, bool>>? Enabled,
    int? ReminderLeadHours,
    string? QuietHoursStart,
    string? QuietHoursEnd,
    int? DailySmsCap,
    string? MorningSummaryTime);

public sealed record TemplateUpdateRequest(string? Subject, string? Body);
public sealed record TemplatePreviewRequest(string? Subject, string? Body);
