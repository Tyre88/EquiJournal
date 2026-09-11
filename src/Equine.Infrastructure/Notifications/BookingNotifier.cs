using System.Globalization;
using Equine.Domain.Entities;
using Equine.Infrastructure.Practice;
using Equine.Infrastructure.Tokens;
using Equine.Infrastructure.Widget;
using Microsoft.EntityFrameworkCore;

namespace Equine.Infrastructure.Notifications;

public sealed class BookingNotifier
{
    private readonly INotificationScheduler _scheduler;
    private readonly NotificationSettingsService _settings;
    private readonly WidgetSettingsService _widget;
    private readonly PracticeSettingsService _practice;
    private readonly PublicBookingTokenService _tokens;
    private readonly EquineDbContext _db;

    public BookingNotifier(
        INotificationScheduler scheduler,
        NotificationSettingsService settings,
        WidgetSettingsService widget,
        PracticeSettingsService practice,
        PublicBookingTokenService tokens,
        EquineDbContext db)
    {
        _scheduler = scheduler;
        _settings = settings;
        _widget = widget;
        _practice = practice;
        _tokens = tokens;
        _db = db;
    }

    public async Task OnWidgetRequestedAsync(BookingLine line, string ownerEmail, string ownerName, CancellationToken cancellationToken = default)
    {
        var payload = await PayloadAsync(line, ownerName, cancellationToken);
        await _scheduler.EnqueueAsync(NotificationType.BookingVerification, NotificationChannel.Email, ownerEmail,
            nameof(BookingLine), line.Id, DateTimeOffset.UtcNow, payload, cancellationToken);

        var practice = await _practice.GetAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(practice.Email))
        {
            await _scheduler.EnqueueAsync(NotificationType.BookingRequestedPractitioner, NotificationChannel.Email, practice.Email,
                nameof(BookingLine), line.Id, DateTimeOffset.UtcNow, payload, cancellationToken);
        }
        if (!string.IsNullOrWhiteSpace(practice.Phone))
        {
            await _scheduler.EnqueueAsync(NotificationType.BookingRequestedPractitioner, NotificationChannel.Sms, practice.Phone,
                nameof(BookingLine), line.Id, DateTimeOffset.UtcNow, payload, cancellationToken);
        }
        var practitionerId = line.Visit.PractitionerId;
        await _scheduler.EnqueueAsync(NotificationType.BookingRequestedPractitioner, NotificationChannel.InApp, practitionerId.ToString(),
            nameof(BookingLine), line.Id, DateTimeOffset.UtcNow, payload, cancellationToken);
    }

    public async Task OnConfirmedAsync(BookingLine line, string ownerEmail, string ownerName, CancellationToken cancellationToken = default)
    {
        var payload = await PayloadAsync(line, ownerName, cancellationToken);
        payload["bookingLineId"] = line.Id.ToString();
        payload["icsSequence"] = "0";
        await _scheduler.EnqueueAsync(NotificationType.BookingConfirmed, NotificationChannel.Email, ownerEmail,
            nameof(BookingLine), line.Id, DateTimeOffset.UtcNow, payload, cancellationToken);
        if (!string.IsNullOrWhiteSpace(line.Owner?.Phone))
        {
            await _scheduler.EnqueueAsync(NotificationType.BookingConfirmed, NotificationChannel.Sms, line.Owner.Phone,
                nameof(BookingLine), line.Id, DateTimeOffset.UtcNow, payload, cancellationToken);
        }
        await ScheduleReminderAsync(line, ownerEmail, payload, cancellationToken);
    }

    public async Task OnRescheduledAsync(BookingLine line, string ownerEmail, string ownerName, CancellationToken cancellationToken = default)
    {
        await _scheduler.CancelPendingAsync(NotificationType.Reminder24h, line.Id, cancellationToken);
        await _scheduler.CancelPendingAsync(NotificationType.BookingConfirmed, line.Id, cancellationToken);
        var payload = await PayloadAsync(line, ownerName, cancellationToken);
        payload["bookingLineId"] = line.Id.ToString();
        var prior = await _db.NotificationLog.CountAsync(
            l => l.RelatedEntityId == line.Id && l.Type == NotificationType.BookingRescheduled, cancellationToken);
        payload["icsSequence"] = (prior + 1).ToString();
        await _scheduler.EnqueueAsync(NotificationType.BookingRescheduled, NotificationChannel.Email, ownerEmail,
            nameof(BookingLine), line.Id, DateTimeOffset.UtcNow, payload, cancellationToken);
        if (!string.IsNullOrWhiteSpace(line.Owner?.Phone))
        {
            await _scheduler.EnqueueAsync(NotificationType.BookingRescheduled, NotificationChannel.Sms, line.Owner.Phone,
                nameof(BookingLine), line.Id, DateTimeOffset.UtcNow, payload, cancellationToken);
        }
        await ScheduleReminderAsync(line, ownerEmail, payload, cancellationToken);
    }

    public async Task OnCancelledAsync(BookingLine line, CancellationToken cancellationToken = default)
    {
        await _scheduler.CancelPendingAsync(line.Id, cancellationToken);
        var ownerName = line.Owner?.Name ?? "";
        var payload = await PayloadAsync(line, ownerName, cancellationToken);
        if (!string.IsNullOrWhiteSpace(line.Owner?.Email))
        {
            await _scheduler.EnqueueAsync(NotificationType.BookingCancelled, NotificationChannel.Email, line.Owner.Email,
                nameof(BookingLine), line.Id, DateTimeOffset.UtcNow, payload, cancellationToken);
        }
        var practice = await _practice.GetAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(practice.Email))
        {
            await _scheduler.EnqueueAsync(NotificationType.BookingCancelled, NotificationChannel.Email, practice.Email,
                nameof(BookingLine), line.Id, DateTimeOffset.UtcNow, payload, cancellationToken);
        }
    }

    public async Task OnPractitionerApprovalNeededAsync(BookingLine line, CancellationToken cancellationToken = default)
    {
        var payload = await PayloadAsync(line, line.Owner?.Name ?? "", cancellationToken);
        var practice = await _practice.GetAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(practice.Email))
        {
            await _scheduler.EnqueueAsync(NotificationType.BookingRequestedPractitioner, NotificationChannel.Email, practice.Email,
                nameof(BookingLine), line.Id, DateTimeOffset.UtcNow, payload, cancellationToken);
        }
    }

    private async Task ScheduleReminderAsync(
        BookingLine line,
        string ownerEmail,
        Dictionary<string, string> payload,
        CancellationToken cancellationToken)
    {
        var settings = await _settings.GetAsync(cancellationToken);
        var visit = line.Visit;
        var starts = visit.DeriveLineStart(line);
        var when = starts.AddHours(-settings.ReminderLeadHours);
        if (when <= DateTimeOffset.UtcNow)
            return;

        var phone = line.Owner?.Phone;
        if (!string.IsNullOrWhiteSpace(phone))
        {
            await _scheduler.EnqueueAsync(NotificationType.Reminder24h, NotificationChannel.Sms, phone,
                nameof(BookingLine), line.Id, when, payload, cancellationToken);
        }
        else
        {
            await _scheduler.EnqueueAsync(NotificationType.Reminder24h, NotificationChannel.Email, ownerEmail,
                nameof(BookingLine), line.Id, when, payload, cancellationToken);
        }
    }

    private async Task<Dictionary<string, string>> PayloadAsync(BookingLine line, string ownerName, CancellationToken cancellationToken)
    {
        var widget = await _widget.GetAsync(cancellationToken);
        var practice = await _practice.GetAsync(cancellationToken);
        var visit = line.Visit;
        var starts = visit.DeriveLineStart(line);
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Stockholm");
        var local = TimeZoneInfo.ConvertTime(starts, zone);
        var token = _tokens.Protect(line.Id, starts.AddDays(7), PublicBookingTokenService.ManagePurpose);
        var manage = $"{widget.PublicBaseUrl}/widget/b/{Uri.EscapeDataString(token)}";
        if (line.EmailVerifiedAt is null)
        {
            var verify = _tokens.Protect(line.Id, DateTimeOffset.UtcNow.AddMinutes(widget.VerificationWindowMinutes), PublicBookingTokenService.VerifyPurpose);
            manage = $"{widget.PublicBaseUrl}/widget/verify?token={Uri.EscapeDataString(verify)}";
        }

        var loc = visit.Location;
        var address = loc is null
            ? ""
            : string.Join(", ", new[] { loc.AddressStreet, loc.AddressPostcode, loc.AddressCity }.Where(s => !string.IsNullOrWhiteSpace(s)));

        return new Dictionary<string, string>
        {
            ["klientnamn"] = ownerName,
            ["hästnamn"] = line.Horse?.Name ?? "",
            ["hastnamn"] = line.Horse?.Name ?? "",
            ["behandling"] = line.TreatmentName,
            ["datum"] = local.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["tid"] = local.ToString("HH:mm", CultureInfo.InvariantCulture),
            ["adress"] = address,
            ["avbokningslänk"] = manage,
            ["avbokningslank"] = manage,
            ["kliniknamn"] = practice.Clinic,
            ["telefon"] = practice.Phone,
            ["referens"] = line.PublicReference ?? "",
            ["portalänk"] = $"{widget.PublicBaseUrl}/portal/",
            ["portallank"] = $"{widget.PublicBaseUrl}/portal/"
        };
    }
}
