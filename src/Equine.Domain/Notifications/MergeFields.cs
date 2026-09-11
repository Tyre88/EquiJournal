namespace Equine.Domain.Notifications;

public static class MergeFields
{
    public static readonly IReadOnlySet<string> Allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "klientnamn",
        "hästnamn",
        "hastnamn",
        "behandling",
        "datum",
        "tid",
        "adress",
        "avbokningslänk",
        "avbokningslank",
        "kliniknamn",
        "telefon",
        "referens",
        "portalänk",
        "portallank",
        "magiclänk",
        "magiclank"
    };

    public static IReadOnlyList<string> UnknownFields(string text)
    {
        var unknown = new List<string>();
        if (string.IsNullOrEmpty(text)) return unknown;
        var start = 0;
        while (true)
        {
            var open = text.IndexOf("{{", start, StringComparison.Ordinal);
            if (open < 0) break;
            var close = text.IndexOf("}}", open + 2, StringComparison.Ordinal);
            if (close < 0) break;
            var field = text[(open + 2)..close].Trim();
            if (field.Length > 0 && !Allowed.Contains(field))
                unknown.Add(field);
            start = close + 2;
        }
        return unknown;
    }

    public static string Render(string template, IReadOnlyDictionary<string, string> values)
    {
        if (string.IsNullOrEmpty(template)) return string.Empty;
        var result = template;
        foreach (var (key, value) in values)
            result = result.Replace("{{" + key + "}}", value, StringComparison.OrdinalIgnoreCase);
        return result;
    }
}
