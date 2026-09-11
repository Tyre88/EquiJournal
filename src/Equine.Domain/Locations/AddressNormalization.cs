namespace Equine.Domain.Locations;

public static class AddressNormalization
{
    public static string NormalizePostcode(string? postcode)
    {
        if (string.IsNullOrWhiteSpace(postcode)) return string.Empty;
        return new string(postcode.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
    }

    public static bool IsValidSwedishPostcode(string? postcode)
    {
        var normalized = NormalizePostcode(postcode);
        return normalized.Length == 5 && normalized.All(char.IsDigit);
    }

    public static string FormatSwedishPostcode(string? postcode)
    {
        var normalized = NormalizePostcode(postcode);
        if (normalized.Length != 5) return normalized;
        return $"{normalized[..3]} {normalized[3..]}";
    }

    public static string FormatDisplay(string? street, string? postcode, string? city)
    {
        var locality = string.Join(" ", new[] { FormatSwedishPostcode(postcode), city?.Trim() }
            .Where(p => !string.IsNullOrWhiteSpace(p)));
        return string.Join(", ", new[] { street?.Trim(), locality }.Where(p => !string.IsNullOrWhiteSpace(p)));
    }

    public static string LocationKey(string? street, string? postcode, string? city)
    {
        var s = (street ?? string.Empty).Trim().ToLowerInvariant();
        var p = NormalizePostcode(postcode);
        var c = (city ?? string.Empty).Trim().ToLowerInvariant();
        return $"{s}|{p}|{c}";
    }

    public static bool IsEmptyKey(string? key) =>
        string.IsNullOrWhiteSpace(key) || key == "||";
}
