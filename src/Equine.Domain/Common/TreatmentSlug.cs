using System.Globalization;
using System.Text;

namespace Equine.Domain.Common;

public static class TreatmentSlug
{
    public static string FromName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "behandling";

        var folded = name.Trim().ToLowerInvariant()
            .Replace('å', 'a').Replace('ä', 'a').Replace('ö', 'o')
            .Replace('é', 'e').Replace('ü', 'u');

        var sb = new StringBuilder(folded.Length);
        var dash = false;
        foreach (var ch in folded.Normalize(NormalizationForm.FormD))
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsAsciiLetterOrDigit(ch))
            {
                sb.Append(ch);
                dash = false;
            }
            else if (!dash && sb.Length > 0)
            {
                sb.Append('-');
                dash = true;
            }
        }

        var slug = sb.ToString().Trim('-');
        if (slug.Length == 0) slug = "behandling";
        return slug.Length > 60 ? slug[..60].Trim('-') : slug;
    }
}
