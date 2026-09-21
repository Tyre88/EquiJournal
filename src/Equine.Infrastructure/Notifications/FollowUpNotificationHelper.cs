using Equine.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Equine.Infrastructure.Notifications;

public static class FollowUpNotificationHelper
{
    public static async Task<Dictionary<string, string>> BuildPayloadAsync(
        Horse horse,
        string clinicName,
        string publicBaseUrl,
        string tenantSlug,
        EquineDbContext db,
        CancellationToken ct = default)
    {
        var last = horse.JournalEntries
            .Where(j => j.Status == JournalStatus.Signed)
            .OrderByDescending(j => j.PerformedAt)
            .FirstOrDefault();

        var treatmentSlug = "";
        if (last?.TreatmentTypeId is Guid tid)
        {
            var treatment = await db.TreatmentTypes.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == tid, ct);
            treatmentSlug = treatment?.Slug ?? "";
        }

        var portalSlug = string.IsNullOrWhiteSpace(tenantSlug) ? "default" : tenantSlug;
        var portalLink = $"{publicBaseUrl}/portal/{portalSlug}";
        var bookingLink = string.IsNullOrWhiteSpace(treatmentSlug)
            ? $"{publicBaseUrl}/widget/"
            : $"{publicBaseUrl}/widget/?behandling={Uri.EscapeDataString(treatmentSlug)}";

        return new Dictionary<string, string>
        {
            ["klientnamn"] = horse.Owner.Name,
            ["hästnamn"] = horse.Name,
            ["hastnamn"] = horse.Name,
            ["behandling"] = last?.TreatmentTypeName ?? "",
            ["kliniknamn"] = clinicName,
            ["portalänk"] = portalLink,
            ["portallank"] = portalLink,
            ["bokningslänk"] = bookingLink,
            ["bokningslank"] = bookingLink
        };
    }
}
