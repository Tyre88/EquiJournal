using Equine.Domain.Entities;

namespace Equine.Infrastructure.Notifications;

public static class FollowUpNotificationHelper
{
    public static Dictionary<string, string> BuildPayload(
        Horse horse,
        string clinicName,
        string publicBaseUrl)
    {
        var last = horse.JournalEntries
            .Where(j => j.Status == JournalStatus.Signed)
            .OrderByDescending(j => j.PerformedAt)
            .FirstOrDefault();

        return new Dictionary<string, string>
        {
            ["klientnamn"] = horse.Owner.Name,
            ["hästnamn"] = horse.Name,
            ["hastnamn"] = horse.Name,
            ["behandling"] = last?.TreatmentTypeName ?? "",
            ["kliniknamn"] = clinicName,
            ["portalänk"] = $"{publicBaseUrl}/portal/",
            ["portallank"] = $"{publicBaseUrl}/portal/"
        };
    }
}
