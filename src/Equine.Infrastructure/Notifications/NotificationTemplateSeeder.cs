using Equine.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Equine.Infrastructure.Notifications;

public static class NotificationTemplateSeeder
{
    public static async Task SeedAsync(EquineDbContext db, CancellationToken cancellationToken = default)
    {
        var existing = await db.NotificationTemplates
            .Select(t => new { t.Type, t.Channel })
            .ToListAsync(cancellationToken);
        var have = existing.Select(t => (t.Type, t.Channel)).ToHashSet();
        var all = new[]
        {
            T(NotificationType.BookingVerification, NotificationChannel.Email, "Bekräfta din bokning",
                "Hej {{klientnamn}},\n\nBekräfta din bokning av {{behandling}} genom att öppna länken:\n{{avbokningslänk}}\n\nReferens: {{referens}}\n\nHälsningar\n{{kliniknamn}}"),
            T(NotificationType.BookingRequestedPractitioner, NotificationChannel.Email, "Ny bokningsförfrågan",
                "Ny bokningsförfrågan väntar på godkännande.\n\nBehandling: {{behandling}}\nTid: {{datum}} {{tid}}\nReferens: {{referens}}"),
            T(NotificationType.BookingRequestedPractitioner, NotificationChannel.Sms,
                "", "Ny förfrågan: {{behandling}} {{datum}} {{tid}}. Ref {{referens}}"),
            T(NotificationType.BookingRequestedPractitioner, NotificationChannel.InApp,
                "Ny bokningsförfrågan", "{{behandling}} {{datum}} {{tid}}"),
            T(NotificationType.BookingConfirmed, NotificationChannel.Email, "Din bokning är bekräftad",
                "Hej {{klientnamn}},\n\nDin tid är bekräftad.\n\nHäst: {{hästnamn}}\nBehandling: {{behandling}}\nTid: {{datum}} {{tid}}\nPlats: {{adress}}\nReferens: {{referens}}\n\nAvboka eller boka om: {{avbokningslänk}}\n\nHälsningar\n{{kliniknamn}}\n{{telefon}}"),
            T(NotificationType.BookingConfirmed, NotificationChannel.Sms,
                "", "Bekräftat: {{behandling}} {{datum}} {{tid}}. Avboka: {{avbokningslänk}}"),
            T(NotificationType.BookingRescheduled, NotificationChannel.Email, "Din bokning har flyttats",
                "Hej {{klientnamn}},\n\nDin tid har flyttats.\n\nHäst: {{hästnamn}}\nBehandling: {{behandling}}\nNy tid: {{datum}} {{tid}}\nPlats: {{adress}}\n\nAvboka eller boka om: {{avbokningslänk}}\n\nHälsningar\n{{kliniknamn}}"),
            T(NotificationType.BookingRescheduled, NotificationChannel.Sms,
                "", "Ny tid: {{behandling}} {{datum}} {{tid}}. {{avbokningslänk}}"),
            T(NotificationType.BookingCancelled, NotificationChannel.Email, "Bokning avbokad",
                "Hej {{klientnamn}},\n\nBokningen {{behandling}} {{datum}} {{tid}} är avbokad.\nReferens: {{referens}}\n\nHälsningar\n{{kliniknamn}}"),
            T(NotificationType.Reminder24h, NotificationChannel.Sms,
                "", "Påminnelse: {{behandling}} imorgon {{tid}} hos {{kliniknamn}}. {{avbokningslänk}}"),
            T(NotificationType.Reminder24h, NotificationChannel.Email, "Påminnelse om din tid",
                "Hej {{klientnamn}},\n\nPåminnelse: {{behandling}} för {{hästnamn}} {{datum}} {{tid}}.\nPlats: {{adress}}\n\n{{avbokningslänk}}"),
            T(NotificationType.DailySummary, NotificationChannel.Email, "Dagens schema",
                "Här är dagens schema:\n\n{{behandling}}\n\nHälsningar\n{{kliniknamn}}"),
            T(NotificationType.FollowUpDue, NotificationChannel.Email, "Dags för uppföljning",
                "Hej {{klientnamn}},\n\nDet är dags för uppföljning av {{behandling}} för {{hästnamn}}.\nBoka tid: {{portalänk}}\n\nHälsningar\n{{kliniknamn}}"),
            T(NotificationType.JournalUnsigned, NotificationChannel.Email, "Osignerad journal",
                "En journal har varit osignerad mer än 24 timmar ({{hästnamn}}, {{behandling}})."),
            T(NotificationType.JournalUnsigned, NotificationChannel.InApp, "Osignerad journal",
                "{{hästnamn}} — {{behandling}}"),
            T(NotificationType.NotificationFailed, NotificationChannel.Email, "Avisering misslyckades",
                "En avisering till {{klientnamn}} kunde inte levereras efter flera försök."),
            T(NotificationType.NotificationFailed, NotificationChannel.InApp, "Avisering misslyckades",
                "Leverans till {{klientnamn}} misslyckades."),
            T(NotificationType.MagicLink, NotificationChannel.Email, "Logga in hos {{kliniknamn}}",
                "Hej {{klientnamn}},\n\nÖppna länken för att se dina bokningar (giltig 15 minuter):\n{{magiclänk}}\n\nHälsningar\n{{kliniknamn}}"),
            T(NotificationType.MarketingBroadcast, NotificationChannel.Email, "{{behandling}}",
                "Hej {{klientnamn}},\n\n{{behandling}}\n\nAvregistrera: {{avbokningslänk}}"),
            T(NotificationType.WeeklyDigest, NotificationChannel.Email, "Veckosammanfattning",
                "Hej,\n\n{{behandling}}\n\nHälsningar\n{{kliniknamn}}")
        };

        foreach (var template in all)
        {
            if (!have.Contains((template.Type, template.Channel)))
                db.NotificationTemplates.Add(template);
        }

        if (db.ChangeTracker.HasChanges())
            await db.SaveChangesAsync(cancellationToken);
    }

    private static NotificationTemplate T(NotificationType type, NotificationChannel channel, string subject, string body) =>
        new(type, channel, subject, body);
}
