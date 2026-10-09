using System.Globalization;
using System.IO;
using Microsoft.VisualBasic.FileIO;

namespace KhitunGeo.Native;

internal static class TabularPaste
{
    public static IReadOnlyList<SurveyPoint> Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return Array.Empty<SurveyPoint>();
        var rows = ParseCells(text);
        var first = Array.FindIndex(rows, row => row.Any(field => !string.IsNullOrWhiteSpace(field)));
        if (first < 0) return Array.Empty<SurveyPoint>();
        var header = rows[first].Select(Header).ToArray();
        var x = Find(header, "x", "e", "east", "easting", "восток", "восточная координата");
        var y = Find(header, "y", "n", "north", "northing", "север", "северная координата");
        var hasHeader = x >= 0 && y >= 0;
        var name = Find(header, "name", "point", "point name", "point no", "number", "no", "id", "номер", "имя", "точка", "№", "№ точки");
        var height = Find(header, "z", "h", "z/h", "z h", "height", "elev", "elevation", "altitude", "высота", "отметка");
        var description = Find(header, "description", "desc", "code", "note", "comment", "описание", "код");
        var start = hasHeader ? first + 1 : first;
        var result = new List<SurveyPoint>();
        for (var rowIndex = start; rowIndex < rows.Length; rowIndex++)
        {
            var fields = rows[rowIndex];
            if (fields.All(string.IsNullOrWhiteSpace)) continue;
            if (hasHeader)
            {
                result.Add(new SurveyPoint(Value(fields, name, (result.Count + 1).ToString()),
                    Number(Value(fields, x)), Number(Value(fields, y)), Number(Value(fields, height)), Value(fields, description)));
                continue;
            }
            if (fields.Length is < 3 or > 5)
                throw new FormatException("Нужны столбцы: имя точки, X, Y; высота и описание необязательны.");
            result.Add(new SurveyPoint(fields[0], Number(fields[1]), Number(fields[2]),
                fields.Length > 3 ? Number(fields[3]) : null, fields.Length > 4 ? fields[4] : ""));
        }
        return result;
    }

    public static string[][] ParseCells(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return Array.Empty<string[]>();
        var separator = DetectCellDelimiter(text);
        using var parser = new TextFieldParser(new StringReader(text));
        parser.SetDelimiters(separator);
        parser.HasFieldsEnclosedInQuotes = true;
        parser.TrimWhiteSpace = false;
        var rows = new List<string[]>();
        while (!parser.EndOfData)
        {
            try { rows.Add(parser.ReadFields() ?? Array.Empty<string>()); }
            catch (MalformedLineException ex) { throw new FormatException("Строка содержит незакрытые кавычки.", ex); }
        }
        return rows.ToArray();
    }

    public static SurveyPoint[] ApplyCells(IReadOnlyList<SurveyPoint> points, string[][] rows, int startRow, int startColumn)
    {
        if (startRow < 0 || startColumn < 0 || startColumn >= 5 || rows.Any(row => startColumn + row.Length > 5))
            throw new FormatException("Вставляемые данные выходят за пределы столбцов таблицы.");
        var next = points.ToList();
        for (var r = 0; r < rows.Length; r++)
        {
            var rowIndex = startRow + r;
            while (next.Count <= rowIndex) next.Add(new SurveyPoint((next.Count + 1).ToString(), null, null, null, ""));
            foreach (var (value, offset) in rows[r].Select((value, offset) => (value, offset)))
            {
                var cell = startColumn + offset;
                var point = next[rowIndex];
                next[rowIndex] = cell switch
                {
                    0 => point with { Name = value },
                    1 => point with { X = Number(value) },
                    2 => point with { Y = Number(value) },
                    3 => point with { Height = Number(value) },
                    _ => point with { Description = value }
                };
            }
        }
        return next.ToArray();
    }

    public static double? Number(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        if (!double.TryParse(text.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || !double.IsFinite(value))
            throw new FormatException("Invalid coordinate: " + text);
        return value;
    }

    private static string DetectCellDelimiter(string text)
    {
        var first = text.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? text;
        foreach (var delimiter in new[] { "\t", ";", "," })
        {
            using var probe = new TextFieldParser(new StringReader(first));
            probe.SetDelimiters(delimiter);
            probe.HasFieldsEnclosedInQuotes = true;
            probe.TrimWhiteSpace = false;
            try { if (probe.ReadFields()?.Length >= 3) return delimiter; }
            catch (MalformedLineException ex) { throw new FormatException("Строка содержит незакрытые кавычки.", ex); }
        }
        return "\t";
    }

    private static string Header(string value) => value.Trim().TrimStart('\uFEFF').ToLowerInvariant().Replace("ё", "е");
    private static int Find(string[] headers, params string[] names) => Array.FindIndex(headers, names.Contains);
    private static string Value(string[] row, int index, string fallback = "") => index >= 0 && index < row.Length ? row[index] : fallback;
}
