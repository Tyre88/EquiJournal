using System.Text.Json;

namespace Equine.Domain.JournalTemplates;

public static class JournalTemplateValidator
{
    public static readonly HashSet<string> AllowedFieldTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "text", "textarea", "number", "select", "multiselect", "checkbox", "date", "anatomy-map"
    };

    public static readonly HashSet<string> AllowedAnatomyPresets = new(StringComparer.OrdinalIgnoreCase)
    {
        "horse-muscles-standard",
        "horse-skeleton-standard"
    };

    public static bool TryValidate(string? json, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(json))
            return true;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                error = "Journal template must be a JSON object.";
                return false;
            }

            if (!doc.RootElement.TryGetProperty("version", out var version) || version.ValueKind != JsonValueKind.Number)
            {
                error = "Journal template must include a numeric version.";
                return false;
            }

            if (!doc.RootElement.TryGetProperty("sections", out var sections) || sections.ValueKind != JsonValueKind.Array)
            {
                error = "Journal template must include a sections array.";
                return false;
            }

            var keys = new HashSet<string>(StringComparer.Ordinal);
            var index = 0;
            foreach (var section in sections.EnumerateArray())
            {
                if (section.ValueKind != JsonValueKind.Object)
                {
                    error = $"Section {index} must be an object.";
                    return false;
                }

                if (!section.TryGetProperty("key", out var keyEl) || keyEl.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(keyEl.GetString()))
                {
                    error = $"Section {index} must have a non-empty key.";
                    return false;
                }

                var key = keyEl.GetString()!;
                if (!keys.Add(key))
                {
                    error = $"Duplicate section key '{key}'.";
                    return false;
                }

                if (!section.TryGetProperty("label", out var labelEl) || labelEl.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(labelEl.GetString()))
                {
                    error = $"Section '{key}' must have a non-empty label.";
                    return false;
                }

                if (!section.TryGetProperty("type", out var typeEl) || typeEl.ValueKind != JsonValueKind.String)
                {
                    error = $"Section '{key}' must have a type.";
                    return false;
                }

                var type = typeEl.GetString()!;
                if (!AllowedFieldTypes.Contains(type))
                {
                    error = $"Section '{key}' has unsupported type '{type}'.";
                    return false;
                }

                if (type is "select" or "multiselect")
                {
                    if (!section.TryGetProperty("options", out var options) || options.ValueKind != JsonValueKind.Array || options.GetArrayLength() == 0)
                    {
                        error = $"Section '{key}' of type {type} must have a non-empty options array.";
                        return false;
                    }
                }

                if (type.Equals("anatomy-map", StringComparison.OrdinalIgnoreCase))
                {
                    var preset = "horse-muscles-standard";
                    if (section.TryGetProperty("preset", out var presetEl) && presetEl.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(presetEl.GetString()))
                        preset = presetEl.GetString()!;

                    if (!AllowedAnatomyPresets.Contains(preset))
                    {
                        error = $"Section '{key}' has unknown anatomy-map preset '{preset}'.";
                        return false;
                    }

                    if (section.TryGetProperty("findingOptions", out var findings))
                    {
                        var hasFindings = findings.ValueKind == JsonValueKind.Array && findings.GetArrayLength() > 0
                            || findings.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(findings.GetString());
                        if (!hasFindings)
                        {
                            error = $"Section '{key}' of type anatomy-map must have a non-empty findingOptions array.";
                            return false;
                        }
                    }
                }

                index++;
            }

            return true;
        }
        catch (JsonException)
        {
            error = "Journal template is not valid JSON.";
            return false;
        }
    }
}
