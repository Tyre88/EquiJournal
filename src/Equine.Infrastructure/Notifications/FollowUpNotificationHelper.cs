using Equine.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Equine.Infrastructure.Notifications;

public static class FollowUpNotificationHelper
{
    public static async Task<Dictionary<string, string>> BuildPayloadAsync(
        Horse horse,
        string clinicName,
        string publicBaseUrl,
        EquineDbContext db,
        CancellationToken ct = default)
    {
        var last = horse.JournalEntries
            .Where(j => j.Status == JournalStatus.Signed)
            .OrderByDescending(j => j.PerformedAt)
            .FirstOrDefault();

        var slug = "";
        if (last?.TreatmentTypeId is Guid tid)
        {
            var treatment = await db.TreatmentTypes.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == tid, ct);
            slug = treatment?.Slug ?? "";
        }

        var bookingLink = string.IsNullOrWhiteSpace(slug)
            ? $"{publicBaseUrl}/widget/"
            : $"{publicBaseUrl}/widget/?behandling={Uri.EscapeDataString(slug)}";

        return new Dictionary<string, string>
        {
            ["klientnamn"] = horse.Owner.Name,
            ["hästnamn"] = horse.Name,
            ["hastnamn"] = horse.Name,
            ["behandling"] = last?.TreatmentTypeName ?? "",
            ["kliniknamn"] = clinicName,
            ["portalänk"] = $"{publicBaseUrl}/portal/",
            ["portallank"] = $"{publicBaseUrl}/portal/",
            ["bokningslänk"] = bookingLink,
            ["bokningslank"] = bookingLink
        };
    }
}
