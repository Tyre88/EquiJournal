using System.Text.Json;
using Equine.Domain.Entities;
using Equine.Domain.Notifications;
using Equine.Infrastructure.Email;
using Equine.Infrastructure.Practice;
using Equine.Infrastructure.Sms;
using Equine.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Equine.Infrastructure.Notifications;

public sealed class NotificationDispatcher
{
    private readonly EquineDbContext _db;
    private readonly IEmailSender _email;
    private readonly ISmsSender _sms;
    private readonly NotificationSettingsService _settings;
    private readonly PracticeSettingsService _practice;
    private readonly IWebPushSender _push;
    private readonly ITenantContext _tenant;
    private readonly TenantProvisioningService _provisioning;

    public NotificationDispatcher(
        EquineDbContext db,
        IEmailSender email,
        ISmsSender sms,
        NotificationSettingsService settings,
        PracticeSettingsService practice,
        IWebPushSender push,
        ITenantContext tenant,
        TenantProvisioningService provisioning)
    {
        _db = db;
        _email = email;
        _sms = sms;
        _settings = settings;
        _practice = practice;
        _push = push;
        _tenant = tenant;
        _provisioning = provisioning;
    }

    public async Task<int> DispatchDueAsync(CancellationToken cancellationToken = default)
    {
        if (!_tenant.HasTenant)
        {
            var total = 0;
            await _provisioning.ForEachActiveAsync(async (_, ct) =>
            {
                total += await DispatchDueForCurrentTenantAsync(ct);
            }, cancellationToken);
            return total;
        }

        return await DispatchDueForCurrentTenantAsync(cancellationToken);
    }

    private async Task<int> DispatchDueForCurrentTenantAsync(CancellationToken cancellationToken)
    {
        var settings = await _settings.GetAsync(cancellationToken);
        var dueIds = await ClaimDueIdsAsync(cancellationToken);

        var count = 0;
        foreach (var id in dueIds)
        {
            var row = await _db.ScheduledNotifications.FirstOrDefaultAsync(n => n.Id == id, cancellationToken);
            if (row is null || row.Status is not (NotificationDeliveryStatus.Pending or NotificationDeliveryStatus.Sending))
                continue;

            if (row.Channel == NotificationChannel.Sms)
            {
                var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(
                    DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Europe/Stockholm")).DateTime);
                var startUtc = TimeZoneInfo.ConvertTimeToUtc(
                    DateTime.SpecifyKind(today.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified),
                    TimeZoneInfo.FindSystemTimeZoneById("Europe/Stockholm"));
                var sentToday = await _db.NotificationLog.CountAsync(
                    l => l.Channel == NotificationChannel.Sms && l.CreatedAt >= startUtc && l.Status == "Sent",
                    cancellationToken);
                if (sentToday >= settings.DailySmsCap)
                {
                    await AlertCapAsync(cancellationToken);
                    continue;
                }
            }

            try
            {
                await DispatchOneAsync(row, cancellationToken);
                count++;
            }
            catch (Exception ex)
            {
                row.MarkAttempt(ex.Message);
                _db.NotificationLog.Add(new NotificationLog(
                    row.Type, row.Channel, row.Recipient, null, null, null,
                    row.RelatedEntityType, row.RelatedEntityId, "Failed", null, ex.Message));
                await _db.SaveChangesAsync(cancellationToken);
                if (row.Status == NotificationDeliveryStatus.Failed && row.Type != NotificationType.NotificationFailed)
                    await EnqueueFailureAlertAsync(row, cancellationToken);
            }
        }

        return count;
    }

    private async Task DispatchOneAsync(ScheduledNotification row, CancellationToken cancellationToken)
    {
        var values = JsonSerializer.Deserialize<Dictionary<string, string>>(row.PayloadJson)
                     ?? new Dictionary<string, string>();
        var practice = await _practice.GetAsync(cancellationToken);
        values.TryAdd("kliniknamn", practice.Clinic);
        values.TryAdd("telefon", practice.Phone);

        var template = await _db.NotificationTemplates
            .FirstOrDefaultAsync(t => t.Type == row.Type && t.Channel == row.Channel, cancellationToken);
        var subject = values.TryGetValue("subject", out var overrideSubject) && !string.IsNullOrWhiteSpace(overrideSubject)
            ? MergeFields.Render(overrideSubject, values)
            : MergeFields.Render(template?.Subject ?? DefaultSubject(row.Type), values);
        var body = MergeFields.Render(template?.Body ?? values.GetValueOrDefault("body") ?? "", values);
        string? providerId = null;

        if (row.Channel == NotificationChannel.Email)
        {
            byte[]? ics = null;
            if (row.Type is NotificationType.BookingConfirmed or NotificationType.BookingRescheduled
                && Guid.TryParse(values.GetValueOrDefault("bookingLineId"), out var lineId))
            {
                var sequence = 0;
                if (values.TryGetValue("icsSequence", out var rawSeq) && int.TryParse(rawSeq, out var parsed))
                    sequence = parsed;
                else if (row.Type == NotificationType.BookingRescheduled)
                    sequence = 1;
                ics = await BuildIcsAsync(lineId, sequence, values, cancellationToken);
            }

            var listUnsub = values.GetValueOrDefault("avbokningslänk")
                            ?? values.GetValueOrDefault("avbokningslank");
            providerId = await _email.SendAsync(new EmailMessage(
                row.Recipient,
                subject,
                body,
                HtmlBody: ToHtml(body),
                Attachments: ics is null ? null : [new EmailAttachment("bokning.ics", "text/calendar", ics)],
                ReplyTo: practice.Email,
                ListUnsubscribe: string.IsNullOrWhiteSpace(listUnsub) ? null : $"<{listUnsub}>"), cancellationToken);
        }
        else if (row.Channel == NotificationChannel.Sms)
        {
            providerId = await _sms.SendAsync(new SmsMessage(row.Recipient, body), cancellationToken);
        }
        else
        {
            if (Guid.TryParse(row.Recipient, out var userId))
            {
                _db.InAppNotifications.Add(new InAppNotification(userId, row.Type, subject, body, values.GetValueOrDefault("länk")));
                await _push.SendAsync(userId, subject, body, cancellationToken);
            }
        }

        row.MarkSent(providerId);
        _db.NotificationLog.Add(new NotificationLog(
            row.Type, row.Channel, row.Recipient, template?.Id, subject, body,
            row.RelatedEntityType, row.RelatedEntityId, "Sent", providerId, null));
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<byte[]?> BuildIcsAsync(
        Guid lineId,
        int sequence,
        Dictionary<string, string> values,
        CancellationToken cancellationToken)
    {
        var line = await _db.BookingLines
            .Include(l => l.Visit).ThenInclude(v => v.Location)
            .Include(l => l.Visit).ThenInclude(v => v.Lines)
            .FirstOrDefaultAsync(l => l.Id == lineId, cancellationToken);
        if (line?.Visit is null) return null;
        var starts = line.Visit.DeriveLineStart(line);
        var ends = line.Visit.DeriveLineEnd(line);
        var location = values.GetValueOrDefault("adress");
        return IcsBuilder.Build(line.Id, starts, ends, line.TreatmentName, location, values.GetValueOrDefault("avbokningslänk"), sequence);
    }

    private async Task EnqueueFailureAlertAsync(ScheduledNotification failed, CancellationToken cancellationToken)
    {
        var practice = await _practice.GetAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(practice.Email)) return;
        _db.ScheduledNotifications.Add(new ScheduledNotification(
            NotificationType.NotificationFailed,
            NotificationChannel.Email,
            practice.Email,
            nameof(ScheduledNotification),
            failed.Id,
            DateTimeOffset.UtcNow,
            JsonSerializer.Serialize(new Dictionary<string, string>
            {
                ["klientnamn"] = failed.Recipient
            })));
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task AlertCapAsync(CancellationToken cancellationToken)
    {
        var practice = await _practice.GetAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(practice.Email)) return;
        var already = await _db.ScheduledNotifications.AnyAsync(
            n => n.Type == NotificationType.NotificationFailed
                 && n.RelatedEntityType == "SmsCap"
                 && n.CreatedAt > DateTimeOffset.UtcNow.AddHours(-12),
            cancellationToken);
        if (already) return;
        _db.ScheduledNotifications.Add(new ScheduledNotification(
            NotificationType.NotificationFailed,
            NotificationChannel.Email,
            practice.Email,
            "SmsCap",
            Guid.CreateVersion7(),
            DateTimeOffset.UtcNow,
            JsonSerializer.Serialize(new Dictionary<string, string> { ["klientnamn"] = "SMS-tak" })));
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<List<Guid>> ClaimDueIdsAsync(CancellationToken cancellationToken)
    {
        if (!_tenant.HasTenant)
            return [];

        var tenantId = _tenant.TenantId;
        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
            await _db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE scheduled_notifications
                SET "Status" = 'Sending'
                WHERE "Id" IN (
                    SELECT "Id" FROM scheduled_notifications
                    WHERE "TenantId" = {tenantId}
                      AND (
                        ("Status" = 'Pending' AND "ScheduledFor" <= now())
                        OR ("Status" = 'Sending' AND "SentAt" IS NULL)
                      )
                    ORDER BY "ScheduledFor"
                    FOR UPDATE SKIP LOCKED
                    LIMIT 25
                )
                """, cancellationToken);

            var ids = await _db.ScheduledNotifications
                .Where(n => n.Status == NotificationDeliveryStatus.Sending && n.SentAt == null)
                .OrderBy(n => n.ScheduledFor)
                .Select(n => n.Id)
                .Take(25)
                .ToListAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
            return ids;
        });
    }

    private static string ToHtml(string body) =>
        "<div style=\"font-family:system-ui,sans-serif;line-height:1.5;color:#1a1a1a\">"
        + string.Join("", body.Replace("\r\n", "\n").Split('\n')
            .Select(line => "<p>" + System.Net.WebUtility.HtmlEncode(line) + "</p>"))
        + "</div>";

    private static string DefaultSubject(NotificationType type) => type switch
    {
        NotificationType.BookingVerification => "Bekräfta din bokning",
        NotificationType.BookingConfirmed => "Din bokning är bekräftad",
        _ => "Meddelande från HästJournal"
    };
}
