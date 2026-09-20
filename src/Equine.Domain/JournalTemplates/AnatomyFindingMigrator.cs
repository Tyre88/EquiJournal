using System.Text.Json;
using System.Text.Json.Nodes;

namespace Equine.Domain.JournalTemplates;

public static class AnatomyFindingMigrator
{
    public static string? TryMigrateTemplateJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        JsonNode? root;
        try { root = JsonNode.Parse(json); }
        catch (JsonException) { return null; }

        if (root is not JsonObject obj || obj["sections"] is not JsonArray sections)
            return null;

        var changed = false;
        foreach (var section in sections)
        {
            if (section is not JsonObject sectionObj)
                continue;

            var type = sectionObj["type"]?.GetValue<string>();
            if (string.Equals(type, "bodymap", StringComparison.OrdinalIgnoreCase))
            {
                sectionObj["type"] = "anatomy-map";
                if (sectionObj["preset"] is null)
                    sectionObj["preset"] = "horse-muscles-standard";
                if (sectionObj["findingOptions"] is null)
                    sectionObj["findingOptions"] = ToJsonArray(FindingOptionsDefaults.CurrentDefault);
                changed = true;
                continue;
            }

            if (!string.Equals(type, "anatomy-map", StringComparison.OrdinalIgnoreCase))
                continue;

            if (!sectionObj.TryGetPropertyValue("findingOptions", out var findings) || findings is null)
                continue;

            if (findings is JsonArray arr)
            {
                var items = ReadStringArray(arr);
                if (IsLegacyDefault(items))
                {
                    sectionObj["findingOptions"] = ToJsonArray(FindingOptionsDefaults.CurrentDefault);
                    changed = true;
                }
                else if (RenameFindings(items))
                {
                    sectionObj["findingOptions"] = ToJsonArray(items);
                    changed = true;
                }
            }
            else if (findings is JsonValue val && val.TryGetValue<string>(out var csv) && csv is not null)
            {
                var items = SplitCsv(csv);
                if (IsLegacyDefault(items))
                {
                    sectionObj["findingOptions"] = ToJsonArray(FindingOptionsDefaults.CurrentDefault);
                    changed = true;
                }
                else if (RenameFindings(items))
                {
                    sectionObj["findingOptions"] = string.Join(", ", items);
                    changed = true;
                }
            }
        }

        return changed ? root.ToJsonString() : null;
    }

    public static string? TryMigrateJournalDataJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        JsonNode? root;
        try { root = JsonNode.Parse(json); }
        catch (JsonException) { return null; }

        if (root is not JsonObject obj)
            return null;

        var changed = false;
        foreach (var prop in obj.ToList())
        {
            if (TryMigrateBodyMapValue(prop.Value, out var bodyMapReplacement))
            {
                obj[prop.Key] = bodyMapReplacement;
                changed = true;
                continue;
            }

            if (TryMigrateAnatomyValue(prop.Value, out var replacement))
            {
                if (replacement is not null)
                    obj[prop.Key] = replacement;
                changed = true;
            }
        }

        return changed ? root.ToJsonString() : null;
    }

    private static bool TryMigrateBodyMapValue(JsonNode? value, out JsonNode? replacement)
    {
        replacement = null;
        JsonObject? obj = value as JsonObject;
        if (obj is null && value is JsonValue val && val.TryGetValue<string>(out var raw)
            && !string.IsNullOrWhiteSpace(raw) && raw[0] == '{')
        {
            try { obj = JsonNode.Parse(raw) as JsonObject; }
            catch (JsonException) { return false; }
        }

        if (obj is null || obj["markers"] is not JsonArray markers || markers.Count == 0)
            return false;

        var annotations = new JsonArray();
        var index = 0;
        foreach (var marker in markers)
        {
            if (marker is not JsonObject m)
                continue;

            var id = m["id"]?.GetValue<string>() ?? index.ToString();
            var label = m["label"]?.GetValue<string>()?.Trim();
            if (string.IsNullOrWhiteSpace(label))
                label = $"Markering {index + 1}";

            var side = m["side"]?.GetValue<string>() is "R" or "L" ? m["side"]!.GetValue<string>()! : "L";
            var note = m["note"]?.GetValue<string>() ?? "";

            annotations.Add(new JsonObject
            {
                ["regionId"] = $"legacy-bodymap-{id}",
                ["label"] = label,
                ["side"] = side,
                ["finding"] = label,
                ["note"] = note
            });
            index++;
        }

        replacement = new JsonObject
        {
            ["preset"] = "horse-muscles-standard",
            ["annotations"] = annotations,
            ["strokes"] = new JsonArray()
        };
        return true;
    }

    private static bool TryMigrateAnatomyValue(JsonNode? value, out JsonNode? replacement)
    {
        replacement = null;
        if (value is JsonObject obj && obj["annotations"] is JsonArray annotations)
            return RenameAnnotations(annotations);

        if (value is JsonValue val && val.TryGetValue<string>(out var raw)
            && !string.IsNullOrWhiteSpace(raw) && raw[0] == '{')
        {
            try
            {
                var nested = JsonNode.Parse(raw);
                if (nested is JsonObject nestedObj && nestedObj["annotations"] is JsonArray nestedAnns
                    && RenameAnnotations(nestedAnns))
                {
                    replacement = nested.ToJsonString();
                    return true;
                }
            }
            catch (JsonException)
            {
            }
        }

        return false;
    }

    private static bool RenameAnnotations(JsonArray annotations)
    {
        var changed = false;
        foreach (var item in annotations)
        {
            if (item is not JsonObject ann || ann["finding"] is not JsonValue findingVal)
                continue;
            if (!findingVal.TryGetValue<string>(out var finding) || finding != FindingOptionsDefaults.LegacyFindingLabel)
                continue;
            ann["finding"] = FindingOptionsDefaults.CurrentFindingLabel;
            changed = true;
        }
        return changed;
    }

    private static bool IsLegacyDefault(IReadOnlyList<string> items) =>
        items.Count == FindingOptionsDefaults.LegacyDefault.Length
        && items.SequenceEqual(FindingOptionsDefaults.LegacyDefault, StringComparer.Ordinal);

    private static bool RenameFindings(List<string> items)
    {
        var changed = false;
        for (var i = 0; i < items.Count; i++)
        {
            if (items[i] != FindingOptionsDefaults.LegacyFindingLabel)
                continue;
            items[i] = FindingOptionsDefaults.CurrentFindingLabel;
            changed = true;
        }
        return changed;
    }

    private static List<string> ReadStringArray(JsonArray arr)
    {
        var items = new List<string>(arr.Count);
        foreach (var node in arr)
            items.Add(node is JsonValue v && v.TryGetValue<string>(out var s) ? s ?? "" : "");
        return items;
    }

    private static JsonArray ToJsonArray(IEnumerable<string> items)
    {
        var arr = new JsonArray();
        foreach (var item in items)
            arr.Add(item);
        return arr;
    }

    private static List<string> SplitCsv(string csv) =>
        csv.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList();
}
