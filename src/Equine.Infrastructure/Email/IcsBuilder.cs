using System.Globalization;
using System.Text;

namespace Equine.Infrastructure.Email;

public static class IcsBuilder
{
    public static byte[] Build(
        Guid uid,
        DateTimeOffset startsAt,
        DateTimeOffset endsAt,
        string summary,
        string? location,
        string? description,
        int sequence = 0)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Stockholm");
        var startLocal = TimeZoneInfo.ConvertTime(startsAt, zone);
        var endLocal = TimeZoneInfo.ConvertTime(endsAt, zone);
        var stamp = DateTimeOffset.UtcNow;

        var sb = new StringBuilder();
        sb.AppendLine("BEGIN:VCALENDAR");
        sb.AppendLine("VERSION:2.0");
        sb.AppendLine("PRODID:-//HastJournal//Booking//SV");
        sb.AppendLine("CALSCALE:GREGORIAN");
        sb.AppendLine("METHOD:REQUEST");
        sb.AppendLine("BEGIN:VTIMEZONE");
        sb.AppendLine("TZID:Europe/Stockholm");
        sb.AppendLine("BEGIN:DAYLIGHT");
        sb.AppendLine("TZOFFSETFROM:+0100");
        sb.AppendLine("TZOFFSETTO:+0200");
        sb.AppendLine("TZNAME:CEST");
        sb.AppendLine("DTSTART:19700329T020000");
        sb.AppendLine("RRULE:FREQ=YEARLY;BYMONTH=3;BYDAY=-1SU");
        sb.AppendLine("END:DAYLIGHT");
        sb.AppendLine("BEGIN:STANDARD");
        sb.AppendLine("TZOFFSETFROM:+0200");
        sb.AppendLine("TZOFFSETTO:+0100");
        sb.AppendLine("TZNAME:CET");
        sb.AppendLine("DTSTART:19701025T030000");
        sb.AppendLine("RRULE:FREQ=YEARLY;BYMONTH=10;BYDAY=-1SU");
        sb.AppendLine("END:STANDARD");
        sb.AppendLine("END:VTIMEZONE");
        sb.AppendLine("BEGIN:VEVENT");
        sb.AppendLine($"UID:{uid:N}@hastjournal");
        sb.AppendLine($"DTSTAMP:{stamp.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture)}");
        sb.AppendLine($"DTSTART;TZID=Europe/Stockholm:{FormatLocal(startLocal)}");
        sb.AppendLine($"DTEND;TZID=Europe/Stockholm:{FormatLocal(endLocal)}");
        sb.AppendLine($"SUMMARY:{Escape(summary)}");
        if (!string.IsNullOrWhiteSpace(location))
            sb.AppendLine($"LOCATION:{Escape(location)}");
        if (!string.IsNullOrWhiteSpace(description))
            sb.AppendLine($"DESCRIPTION:{Escape(description)}");
        sb.AppendLine($"SEQUENCE:{Math.Max(0, sequence)}");
        sb.AppendLine("END:VEVENT");
        sb.AppendLine("END:VCALENDAR");
        return Encoding.UTF8.GetBytes(sb.ToString().Replace("\r\n", "\n").Replace("\n", "\r\n"));
    }

    private static string FormatLocal(DateTimeOffset local) =>
        local.ToString("yyyyMMdd'T'HHmmss", CultureInfo.InvariantCulture);

    private static string Escape(string value) =>
        value.Replace("\\", "\\\\").Replace(";", "\\;").Replace(",", "\\,").Replace("\n", "\\n");
}
