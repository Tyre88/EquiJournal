using System.Text.Json;
using Equine.Domain.Entities;
using Equine.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Equine.Infrastructure.Notifications;

public sealed class NotificationScheduler : INotificationScheduler
{
    private readonly EquineDbContext _db;
    private readonly NotificationSettingsService _settings;
    private readonly IHostEnvironment _env;
    private readonly IServiceProvider _services;

    public NotificationScheduler(
        EquineDbContext db,
        NotificationSettingsService settings,
        IHostEnvironment env,
        IServiceProvider services)
    {
        _db = db;
        _settings = settings;
        _env = env;
        _services = services;
    }

    public async Task EnqueueAsync(
        NotificationType type,
        NotificationChannel channel,
        string recipient,
        string relatedEntityType,
        Guid relatedEntityId,
        DateTimeOffset scheduledFor,
        IReadOnlyDictionary<string, string> payload,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(recipient) && channel != NotificationChannel.InApp)
            return;

        var settings = await _settings.GetAsync(cancellationToken);
        if (!settings.IsEnabled(type, channel))
            return;

        var when = scheduledFor;
        if (channel == NotificationChannel.Sms)
        {
            var zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Stockholm");
            when = QuietHours.DeferToWindow(when, settings.QuietHoursStart, settings.QuietHoursEnd, zone);
        }

        if (type == NotificationType.FollowUpDue)
        {
            var recent = await _db.NotificationLog.AnyAsync(
                n => n.Type == type
                     && n.RelatedEntityId == relatedEntityId
                     && n.Channel == channel
                     && n.Status == "Sent"
                     && n.CreatedAt >= DateTimeOffset.UtcNow.AddDays(-30),
                cancellationToken);
            if (recent)
                return;
        }

        var existing = await _db.ScheduledNotifications.FirstOrDefaultAsync(
            n => n.Type == type && n.RelatedEntityId == relatedEntityId && n.Channel == channel
                 && n.Status == NotificationDeliveryStatus.Pending,
            cancellationToken);

        var json = JsonSerializer.Serialize(payload);
        if (existing is not null)
        {
            existing.Reschedule(when);
        }
        else
        {
            _db.ScheduledNotifications.Add(new ScheduledNotification(
                type, channel, recipient, relatedEntityType, relatedEntityId, when, json));
        }

        await _db.SaveChangesAsync(cancellationToken);

        if (_env.IsEnvironment("Testing"))
            await _services.GetRequiredService<NotificationDispatcher>().DispatchDueAsync(cancellationToken);
    }

    public async Task CancelPendingAsync(NotificationType type, Guid relatedEntityId, CancellationToken cancellationToken = default)
    {
        var rows = await _db.ScheduledNotifications
            .Where(n => n.Type == type && n.RelatedEntityId == relatedEntityId && n.Status == NotificationDeliveryStatus.Pending)
            .ToListAsync(cancellationToken);
        foreach (var row in rows)
            row.Cancel();
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task CancelPendingAsync(Guid relatedEntityId, CancellationToken cancellationToken = default)
    {
        var rows = await _db.ScheduledNotifications
            .Where(n => n.RelatedEntityId == relatedEntityId && n.Status == NotificationDeliveryStatus.Pending)
            .ToListAsync(cancellationToken);
        foreach (var row in rows)
            row.Cancel();
        await _db.SaveChangesAsync(cancellationToken);
    }
}
