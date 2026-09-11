using System.Globalization;
using System.Text;

namespace Equine.Api.Features.Reports;

internal static class CsvExporter
{
    private static readonly CultureInfo Sv = CultureInfo.GetCultureInfo("sv-SE");

    public static byte[] ToCsv(IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<object?>> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(';', headers.Select(Escape)));
        foreach (var row in rows)
            sb.AppendLine(string.Join(';', row.Select(Format)));
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
    }

    private static string Format(object? value) => Escape(value switch
    {
        null => "",
        DateTimeOffset dto => dto.ToString("yyyy-MM-dd HH:mm", Sv),
        DateOnly d => d.ToString("yyyy-MM-dd", Sv),
        decimal m => m.ToString("0.##", Sv),
        double d => d.ToString("0.##", Sv),
        int i => i.ToString(Sv),
        _ => value.ToString() ?? ""
    });

    private static string Escape(string value)
    {
        if (value.Contains(';') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}
