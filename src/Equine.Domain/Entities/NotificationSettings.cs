using Equine.Domain.Common;
using System.Text.Json;

namespace Equine.Domain.Entities;

public class NotificationSettings : Entity
{
    public static readonly Guid SingletonId = Guid.Parse("00000000-0000-7000-0000-000000000003");

    public string EnabledJson { get; private set; } = "{}";
    public int ReminderLeadHours { get; private set; } = 24;
    public TimeOnly QuietHoursStart { get; private set; } = new(7, 0);
    public TimeOnly QuietHoursEnd { get; private set; } = new(21, 0);
    public int DailySmsCap { get; private set; } = 40;
    public TimeOnly MorningSummaryTime { get; private set; } = new(7, 0);
    public DateTimeOffset UpdatedAt { get; private set; } = DateTimeOffset.UtcNow;

    private NotificationSettings() : base(SingletonId)
    {
        EnabledJson = JsonSerializer.Serialize(DefaultEnabled());
    }

    public static NotificationSettings CreateDefault() => new();

    public bool IsEnabled(NotificationType type, NotificationChannel channel)
    {
        try
        {
            var map = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, bool>>>(EnabledJson)
                      ?? DefaultEnabled();
            if (!map.TryGetValue(type.ToString(), out var channels))
                return true;
            return !channels.TryGetValue(channel.ToString(), out var on) || on;
        }
        catch (JsonException)
        {
            return true;
        }
    }

    public IReadOnlyDictionary<string, Dictionary<string, bool>> GetEnabled()
    {
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, bool>>>(EnabledJson)
                   ?? DefaultEnabled();
        }
        catch (JsonException)
        {
            return DefaultEnabled();
        }
    }

    public void Update(
        Dictionary<string, Dictionary<string, bool>>? enabled = null,
        int? reminderLeadHours = null,
        TimeOnly? quietHoursStart = null,
        TimeOnly? quietHoursEnd = null,
        int? dailySmsCap = null,
        TimeOnly? morningSummaryTime = null)
    {
        if (enabled is not null)
            EnabledJson = JsonSerializer.Serialize(enabled);
        if (reminderLeadHours.HasValue)
            ReminderLeadHours = Math.Clamp(reminderLeadHours.Value, 1, 168);
        if (quietHoursStart.HasValue) QuietHoursStart = quietHoursStart.Value;
        if (quietHoursEnd.HasValue) QuietHoursEnd = quietHoursEnd.Value;
        if (dailySmsCap.HasValue)
            DailySmsCap = Math.Clamp(dailySmsCap.Value, 1, 500);
        if (morningSummaryTime.HasValue) MorningSummaryTime = morningSummaryTime.Value;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static Dictionary<string, Dictionary<string, bool>> DefaultEnabled()
    {
        var map = new Dictionary<string, Dictionary<string, bool>>();
        foreach (var type in Enum.GetValues<NotificationType>())
        {
            map[type.ToString()] = new Dictionary<string, bool>
            {
                [nameof(NotificationChannel.Email)] = true,
                [nameof(NotificationChannel.Sms)] = type is NotificationType.BookingConfirmed
                    or NotificationType.BookingRescheduled
                    or NotificationType.Reminder24h
                    or NotificationType.BookingRequestedPractitioner,
                [nameof(NotificationChannel.InApp)] = type is NotificationType.BookingRequestedPractitioner
                    or NotificationType.JournalUnsigned
                    or NotificationType.NotificationFailed
                    or NotificationType.MarketingBroadcast
            };
        }
        return map;
    }
}
