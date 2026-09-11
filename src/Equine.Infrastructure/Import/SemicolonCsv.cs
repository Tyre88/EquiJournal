using System.Text;

namespace Equine.Infrastructure.Import;

public static class SemicolonCsv
{
    public static IReadOnlyList<IReadOnlyDictionary<string, string>> ReadFile(string path)
    {
        using var stream = File.OpenRead(path);
        using var reader = new StreamReader(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), detectEncodingFromByteOrderMarks: true);
        return Read(reader);
    }

    public static IReadOnlyList<IReadOnlyDictionary<string, string>> Read(TextReader reader)
    {
        var headerLine = reader.ReadLine();
        if (headerLine is null)
            return [];

        var headers = Split(headerLine).Select(NormalizeHeader).ToArray();
        var rows = new List<IReadOnlyDictionary<string, string>>();
        while (reader.ReadLine() is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;
            var cells = Split(line);
            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < headers.Length; i++)
            {
                var value = i < cells.Count ? cells[i].Trim() : "";
                row[headers[i]] = value;
            }
            rows.Add(row);
        }
        return rows;
    }

    internal static string NormalizeHeader(string header) =>
        header.Trim().TrimStart('\uFEFF').ToLowerInvariant();

    internal static List<string> Split(string line)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ';' && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }
        result.Add(current.ToString());
        return result;
    }
}
