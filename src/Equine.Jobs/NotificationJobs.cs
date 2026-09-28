using Equine.Domain.Entities;
using Equine.Infrastructure;
using Equine.Infrastructure.Notifications;
using Equine.Infrastructure.Practice;
using Equine.Infrastructure.Tenancy;
using Equine.Infrastructure.Widget;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Equine.Jobs;

public sealed class NotificationJobs
{
    private readonly IServiceScopeFactory _scopes;

    public NotificationJobs(IServiceScopeFactory scopes) => _scopes = scopes;

    public async Task DispatchDue()
    {
        using var scope = _scopes.CreateScope();
        var work = scope.ServiceProvider.GetRequiredService<TenantProvisioningService>();
        var dispatcher = scope.ServiceProvider.GetRequiredService<NotificationDispatcher>();
        await work.ForEachActiveAsync(async (_, _) => await dispatcher.DispatchDueAsync(), CancellationToken.None);
    }

    public async Task ExpireUnverified()
    {
        using var scope = _scopes.CreateScope();
        var work = scope.ServiceProvider.GetRequiredService<TenantProvisioningService>();
        var service = scope.ServiceProvider.GetRequiredService<ExpireUnverifiedBookingsService>();
        await work.ForEachActiveAsync(async (_, ct) => { await service.RunAsync(ct); }, CancellationToken.None);
    }

    public async Task ScanFollowUps()
    {
        using var scope = _scopes.CreateScope();
        var work = scope.ServiceProvider.GetRequiredService<TenantProvisioningService>();
        await work.ForEachActiveAsync((tenant, ct) => ScanFollowUpsForTenant(scope.ServiceProvider, ct), CancellationToken.None);
    }

    private static async Task ScanFollowUpsForTenant(IServiceProvider services, CancellationToken ct)
    {
        var db = services.GetRequiredService<EquineDbContext>();
        var scheduler = services.GetRequiredService<INotificationScheduler>();
        var tenant = services.GetRequiredService<ITenantContext>();
        var practice = services.GetRequiredService<PracticeSettingsService>();
        var widget = services.GetRequiredService<WidgetSettingsService>();
        var settings = await practice.GetAsync();
        var widgetSettings = await widget.GetAsync();
        var now = DateTimeOffset.UtcNow;
        var dedupCutoff = now.AddDays(-30);
        var horses = await db.Horses
            .Include(h => h.Owner)
            .Include(h => h.JournalEntries)
            .Where(h => h.FollowUpDismissedAt == null)
            .Where(h => h.FollowUpSnoozedUntil == null || h.FollowUpSnoozedUntil < now)
            .ToListAsync(ct);

        foreach (var horse in horses)
        {
            var last = horse.JournalEntries
                .Where(j => j.Status == JournalStatus.Signed)
                .OrderByDescending(j => j.PerformedAt)
                .FirstOrDefault();
            if (last is null) continue;
            var interval = horse.FollowUpOverrideDays
                           ?? (await db.TreatmentTypes.FindAsync(last.TreatmentTypeId))?.FollowUpIntervalDays;
            if (interval is null) continue;
            if (last.PerformedAt.AddDays(interval.Value) > now) continue;

            var recentlySent = await db.NotificationLog.AnyAsync(n =>
                n.Type == NotificationType.FollowUpDue
                && n.RelatedEntityId == horse.Id
                && n.Status == "Sent"
                && n.CreatedAt >= dedupCutoff);
            if (recentlySent) continue;

            var payload = await FollowUpNotificationHelper.BuildPayloadAsync(
                horse, settings.Clinic, widgetSettings.PublicBaseUrl, tenant.Slug ?? "default", db);
            if (!string.IsNullOrWhiteSpace(horse.Owner.Email) && !horse.Owner.EmailInvalid)
            {
                await scheduler.EnqueueAsync(
                    NotificationType.FollowUpDue,
                    NotificationChannel.Email,
                    horse.Owner.Email,
                    nameof(Horse),
                    horse.Id,
                    DateTimeOffset.UtcNow,
                    payload);
            }
        }
    }

    public async Task ScanUnsignedJournals()
    {
        using var scope = _scopes.CreateScope();
        var work = scope.ServiceProvider.GetRequiredService<TenantProvisioningService>();
        await work.ForEachActiveAsync((_, ct) => ScanUnsignedJournalsForTenant(scope.ServiceProvider, ct), CancellationToken.None);
    }

    private static async Task ScanUnsignedJournalsForTenant(IServiceProvider services, CancellationToken ct)
    {
        var db = services.GetRequiredService<EquineDbContext>();
        var scheduler = services.GetRequiredService<INotificationScheduler>();
        var practice = await services.GetRequiredService<PracticeSettingsService>().GetAsync(ct);
        var cutoff = DateTimeOffset.UtcNow.AddHours(-24);
        var drafts = await db.JournalEntries
            .Include(j => j.Horse)
            .Where(j => j.Status == JournalStatus.Draft && j.CreatedAt <= cutoff)
            .ToListAsync(ct);

        foreach (var journal in drafts)
        {
            var payload = new Dictionary<string, string>
            {
                ["hästnamn"] = journal.Horse?.Name ?? "",
                ["hastnamn"] = journal.Horse?.Name ?? "",
                ["behandling"] = journal.TreatmentTypeName ?? "",
                ["kliniknamn"] = practice.Clinic,
                ["länk"] = $"/journals/{journal.Id}/edit"
            };
            if (!string.IsNullOrWhiteSpace(practice.Email))
            {
                await scheduler.EnqueueAsync(
                    NotificationType.JournalUnsigned,
                    NotificationChannel.Email,
                    practice.Email,
                    nameof(JournalEntry),
                    journal.Id,
                    DateTimeOffset.UtcNow,
                    payload);
            }
            if (journal.CreatedBy != Guid.Empty)
            {
                await scheduler.EnqueueAsync(
                    NotificationType.JournalUnsigned,
                    NotificationChannel.InApp,
                    journal.CreatedBy.ToString(),
                    nameof(JournalEntry),
                    journal.Id,
                    DateTimeOffset.UtcNow,
                    payload);
            }
        }
    }

    public async Task SendDailySummary()
    {
        using var scope = _scopes.CreateScope();
        var work = scope.ServiceProvider.GetRequiredService<TenantProvisioningService>();
        await work.ForEachActiveAsync((_, ct) => SendDailySummaryForTenant(scope.ServiceProvider, ct), CancellationToken.None);
    }

    private static async Task SendDailySummaryForTenant(IServiceProvider services, CancellationToken ct)
    {
        var db = services.GetRequiredService<EquineDbContext>();
        var scheduler = services.GetRequiredService<INotificationScheduler>();
        var notify = await services.GetRequiredService<NotificationSettingsService>().GetAsync(ct);
        var practice = await services.GetRequiredService<PracticeSettingsService>().GetAsync(ct);
        if (string.IsNullOrWhiteSpace(practice.Email)) return;

        var zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Stockholm");
        var nowLocal = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone);
        var windowStart = notify.MorningSummaryTime;
        var windowEnd = windowStart.AddMinutes(20);
        var clock = TimeOnly.FromTimeSpan(nowLocal.TimeOfDay);
        if (clock < windowStart || clock >= windowEnd)
            return;

        var today = DateOnly.FromDateTime(nowLocal.DateTime);
        var start = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(today.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified), zone);
        var end = start.AddDays(1);

        var lines = await db.BookingLines
            .Include(l => l.Visit)
            .Include(l => l.Horse)
            .Where(l => l.Visit.StartsAt >= start && l.Visit.StartsAt < end
                        && l.Status == BookingStatus.Confirmed)
            .OrderBy(l => l.Visit.StartsAt)
            .ToListAsync();

        var body = lines.Count == 0
            ? "Inga bekräftade tider idag."
            : string.Join("\n", lines.Select(l =>
            {
                var local = TimeZoneInfo.ConvertTime(l.Visit.DeriveLineStart(l), zone);
                return $"{local:HH:mm} {l.TreatmentName} — {l.Horse?.Name}";
            }));

        var dayId = Guid.Parse(Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes($"daily-summary:{today:yyyy-MM-dd}"))[..16]));
        if (await db.ScheduledNotifications.AnyAsync(n =>
                n.Type == NotificationType.DailySummary && n.RelatedEntityId == dayId))
            return;

        await scheduler.EnqueueAsync(
            NotificationType.DailySummary,
            NotificationChannel.Email,
            practice.Email,
            "Day",
            dayId,
            DateTimeOffset.UtcNow,
            new Dictionary<string, string>
            {
                ["behandling"] = body,
                ["kliniknamn"] = practice.Clinic
            });
    }

    public async Task SendWeeklyDigest()
    {
        using var scope = _scopes.CreateScope();
        var work = scope.ServiceProvider.GetRequiredService<TenantProvisioningService>();
        await work.ForEachActiveAsync((_, ct) => SendWeeklyDigestForTenant(scope.ServiceProvider, ct), CancellationToken.None);
    }

    private static async Task SendWeeklyDigestForTenant(IServiceProvider services, CancellationToken ct)
    {
        var db = services.GetRequiredService<EquineDbContext>();
        var scheduler = services.GetRequiredService<INotificationScheduler>();
        var practice = await services.GetRequiredService<PracticeSettingsService>().GetAsync(ct);
        if (string.IsNullOrWhiteSpace(practice.Email)) return;

        var zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Stockholm");
        var nowLocal = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone);
        if (nowLocal.DayOfWeek != DayOfWeek.Sunday)
            return;

        var weekEnd = DateOnly.FromDateTime(nowLocal.DateTime);
        var weekStart = weekEnd.AddDays(-7);
        var start = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(weekStart.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified), zone);
        var end = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(weekEnd.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified), zone);

        var weekId = Guid.Parse(Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes($"weekly-digest:{weekEnd:yyyy-MM-dd}"))[..16]));
        if (await db.ScheduledNotifications.AnyAsync(n =>
                n.Type == NotificationType.WeeklyDigest && n.RelatedEntityId == weekId))
            return;

        var bookings = await db.BookingLines
            .Include(l => l.Visit)
            .Include(l => l.Horse)
            .Where(l => l.Visit.StartsAt >= start && l.Visit.StartsAt < end)
            .OrderBy(l => l.Visit.StartsAt)
            .ToListAsync();
        var unsigned = await db.JournalEntries.CountAsync(j => j.Status == JournalStatus.Draft);
        var failed = await db.NotificationLog.CountAsync(n =>
            n.Status == "Failed" && n.CreatedAt >= start);
        var bookingLines = bookings.Count == 0
            ? "Inga bokningar den här veckan."
            : string.Join("\n", bookings.Select(l =>
            {
                var local = TimeZoneInfo.ConvertTime(l.Visit.DeriveLineStart(l), zone);
                return $"{local:yyyy-MM-dd HH:mm} {l.TreatmentName} — {l.Horse?.Name} ({l.Status})";
            }));

        var body =
            $"Bokningar {weekStart:yyyy-MM-dd}–{weekEnd.AddDays(-1):yyyy-MM-dd}:\n{bookingLines}\n\n" +
            $"Osignerade journaler: {unsigned}\n" +
            $"Misslyckade aviseringar: {failed}\n" +
            "Senaste databasbackup: kontrollera Dokploy (ska vara nyare än 26 timmar).";

        await scheduler.EnqueueAsync(
            NotificationType.WeeklyDigest,
            NotificationChannel.Email,
            practice.Email,
            "Week",
            weekId,
            DateTimeOffset.UtcNow,
            new Dictionary<string, string>
            {
                ["behandling"] = body,
                ["kliniknamn"] = practice.Clinic
            });
    }

    public async Task PurgeLogs()
    {
        using var scope = _scopes.CreateScope();
        var work = scope.ServiceProvider.GetRequiredService<TenantProvisioningService>();
        await work.ForEachActiveAsync(async (_, ct) =>
        {
            var db = scope.ServiceProvider.GetRequiredService<EquineDbContext>();
            var cutoff = DateTimeOffset.UtcNow.AddMonths(-12);
            var old = await db.NotificationLog.Where(l => l.CreatedAt < cutoff).ToListAsync(ct);
            db.NotificationLog.RemoveRange(old);
            await db.SaveChangesAsync(ct);
        }, CancellationToken.None);
    }

    public async Task CheckDeliverability()
    {
        using var scope = _scopes.CreateScope();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var log = scope.ServiceProvider.GetRequiredService<ILogger<NotificationJobs>>();
        var domain = config["Notifications:SendingDomain"];
        if (string.IsNullOrWhiteSpace(domain)) return;
        var checker = new DnsDeliverabilityChecker();
        var result = await checker.CheckAsync(domain);
        if (!result.Passed)
        {
            log.LogError("Deliverability check failed for {Domain}: SPF={Spf} DMARC={Dmarc} {Error}",
                domain, result.HasSpf, result.HasDmarc, result.Error);
            throw new InvalidOperationException(
                $"Deliverability check failed for {domain}: SPF={result.HasSpf} DMARC={result.HasDmarc} {result.Error}");
        }
    }
}
