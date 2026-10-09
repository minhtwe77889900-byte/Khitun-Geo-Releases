using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace KhitunGeo.Native;

internal sealed record PointImportPreview(
    string FileName,
    string Format,
    string[] Columns,
    string[][] Rows,
    bool HasHeader,
    IReadOnlyList<string> Sheets,
    string? SheetName,
    string? SourceCrsId = null);

internal sealed record PointImportMapping(int Name, int X, int Y, int Height, int Description);
internal sealed record PointImportReview(IReadOnlyList<SurveyPoint> Points, IReadOnlyList<string> Errors, IReadOnlyList<int>? InvalidRows = null);

internal static class PointImportService
{
    private const long MaxTextBytes = 16 * 1024 * 1024;

    public static PointImportPreview Open(string path, string? sheetName = null, string? delimiter = null)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        if (extension == ".xlsx")
        {
            var sheets = ExcelXlsxReader.GetSheetNames(path);
            if (sheets.Count == 0) throw new FormatException("В книге Excel нет листов.");
            var selected = sheets.FirstOrDefault(name => string.Equals(name, sheetName, StringComparison.Ordinal)) ?? sheets[0];
            var rows = ExcelXlsxReader.ReadSheetRows(path, selected);
            return Create(path, "Excel XLSX", rows, sheets, selected);
        }
        if (extension is ".json" or ".geojson")
        {
            var info = new FileInfo(path);
            if (info.Length > MaxTextBytes) throw new FormatException("JSON-файл больше 16 МБ. Разделите файл.");
            var table = JsonPointFileReader.ReadTable(ReadText(path));
            return Create(path, extension == ".geojson" ? "GeoJSON" : "JSON", table.Rows, Array.Empty<string>(), null, table.SourceCrsId);
        }
        if (extension is not (".csv" or ".tsv" or ".txt" or ".xyz" or ".pnt" or ".dat" or ".asc"))
            throw new FormatException("Поддерживаются XLSX, CSV, TSV, TXT, XYZ, PNT, DAT, ASC, JSON и GeoJSON.");
        var fileInfo = new FileInfo(path);
        if (fileInfo.Length > MaxTextBytes) throw new FormatException("Табличный файл больше 16 МБ. Разделите файл.");
        var text = ReadText(path);
        var rows = ParseDelimitedRows(text, delimiter, whitespaceWhenNoDelimiter: extension is ".xyz" or ".pnt" or ".dat" or ".asc");
        return Create(path, extension.TrimStart('.').ToUpperInvariant(), rows, Array.Empty<string>(), null);
    }

    public static PointImportPreview ParseRows(string text, string fileName, string? delimiter = null)
        => Create(fileName, "Табличный текст", ParseDelimitedRows(text, delimiter, whitespaceWhenNoDelimiter: false), Array.Empty<string>(), null);

    private static PointImportPreview Create(string path, string format, string[][] rows, IReadOnlyList<string> sheets, string? sheetName, string? sourceCrsId = null)
    {
        var hasHeader = rows.Length > 0 && PointImportMappingRules.HasCoordinateHeaders(rows[0]);
        var columns = hasHeader
            ? rows[0].Select((value, index) => string.IsNullOrWhiteSpace(value) ? $"Столбец {index + 1}" : value.Trim()).ToArray()
            : (rows.Length == 0 ? Array.Empty<string>() : Enumerable.Range(0, rows.Max(row => row.Length)).Select(i => $"Столбец {ColumnName(i)}").ToArray());
        return new(Path.GetFileName(path), format, columns, rows, hasHeader, sheets, sheetName, sourceCrsId);
    }

    private static string ReadText(string path)
    {
        var bytes = File.ReadAllBytes(path);
        try { return new UTF8Encoding(false, true).GetString(bytes); }
        catch (DecoderFallbackException)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding(1251).GetString(bytes);
        }
    }

    private static string[][] ParseDelimitedRows(string text, string? delimiter, bool whitespaceWhenNoDelimiter)
    {
        if (delimiter == "space")
            return text.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => Regex.Split(line.Trim(), @"\s+"))
                .ToArray();
        if (delimiter is not null) return TabularPaste.ParseCells(text, delimiter);
        if (whitespaceWhenNoDelimiter)
        {
            var first = text.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
            if (!first.Contains('\t') && !first.Contains(';') && Regex.Split(first.Trim(), @"\s+").Length >= 3)
                return text.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(line => Regex.Split(line.Trim(), @"\s+"))
                    .ToArray();
        }
        return TabularPaste.ParseCells(text);
    }

    private static string ColumnName(int index)
    {
        var value = index + 1; var name = "";
        while (value > 0) { value--; name = (char)('A' + value % 26) + name; value /= 26; }
        return name;
    }

    public static PointImportMapping DetectMapping(PointImportPreview preview, bool? hasHeader = null)
        => PointImportMappingRules.Detect(preview.Columns, hasHeader ?? preview.HasHeader, preview.Format);

    public static PointImportReview Map(PointImportPreview preview, PointImportMapping mapping, bool hasHeader, bool swapXY)
    {
        if (mapping.X < 0 || mapping.Y < 0 || mapping.X >= preview.Columns.Length || mapping.Y >= preview.Columns.Length)
            return new(Array.Empty<SurveyPoint>(), new[] { "Назначьте столбцы X и Y." });
        var points = new List<SurveyPoint>(); var errors = new List<string>(); var invalidRows = new List<int>();
        var start = hasHeader ? 1 : 0;
        for (var i = start; i < preview.Rows.Length; i++)
        {
            var row = preview.Rows[i];
            if (row.All(string.IsNullOrWhiteSpace)) continue;
            var xText = Value(row, swapXY ? mapping.Y : mapping.X);
            var yText = Value(row, swapXY ? mapping.X : mapping.Y);
            if (string.IsNullOrWhiteSpace(xText) && string.IsNullOrWhiteSpace(yText)) continue;
            if (!TryNumber(xText, out var x) || !TryNumber(yText, out var y))
            { errors.Add($"Строка {i + 1}: X или Y не является конечным числом."); invalidRows.Add(i - start); continue; }
            var zText = Value(row, mapping.Height);
            if (!string.IsNullOrWhiteSpace(zText) && !TryNumber(zText, out _))
            { errors.Add($"Строка {i + 1}: высота не является конечным числом."); invalidRows.Add(i - start); continue; }
            var height = string.IsNullOrWhiteSpace(zText) ? null : (double?)ParseNumber(zText);
            var name = Value(row, mapping.Name).Trim();
            if (name.Length == 0) name = (points.Count + 1).ToString(CultureInfo.InvariantCulture);
            points.Add(new SurveyPoint(name, x, y, height, Value(row, mapping.Description)));
        }
        if (points.Count == 0 && errors.Count == 0) errors.Add("В выбранном наборе не найдено ни одной точки.");
        return new(points, errors, invalidRows);
    }

    private static string Value(string[] row, int index) => index >= 0 && index < row.Length ? row[index] : "";
    private static bool TryNumber(string text, out double value)
    {
        value = 0;
        return double.TryParse(text.Trim().Replace('−', '-').Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value);
    }
    private static double ParseNumber(string text) => double.Parse(text.Trim().Replace('−', '-').Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture);
}

internal static class PointImportMappingRules
{
    private static readonly string[][] Aliases =
    {
        new[] { "name", "point", "point name", "point no", "number", "no", "id", "номер", "имя", "точка", "№", "№ точки", "p" },
        new[] { "x", "northing", "north", "север", "северная координата", "b", "lat", "latitude", "широта", "n" },
        new[] { "y", "easting", "east", "восток", "восточная координата", "l", "lon", "lng", "longitude", "долгота", "e" },
        new[] { "z", "h", "z/h", "z h", "height", "elev", "elevation", "altitude", "высота", "отметка" },
        new[] { "description", "desc", "code", "note", "comment", "описание", "код", "d" }
    };

    public static bool HasCoordinateHeaders(string[] row)
    {
        var headers = row.Select(Normalize).ToArray();
        return Find(headers, Aliases[1]) >= 0 && Find(headers, Aliases[2]) >= 0;
    }

    public static PointImportMapping Detect(string[] columns, bool hasHeader, string format)
    {
        if (!hasHeader)
        {
            var count = columns.Length;
            var coordinateFile = format is "XYZ" or "PNT" or "DAT" or "ASC";
            if (coordinateFile)
                return new(-1, 0, 1, count > 2 ? 2 : -1, count > 3 ? 3 : -1);
            return count switch
            {
                >= 5 => new(0, 1, 2, 3, 4),
                4 => new(0, 1, 2, 3, -1),
                3 => new(0, 1, 2, -1, -1),
                2 => new(-1, 0, 1, -1, -1),
                _ => new(count > 0 ? 0 : -1, -1, -1, -1, -1)
            };
        }
        var headers = columns.Select(Normalize).ToArray();
        return new(Find(headers, Aliases[0]), Find(headers, Aliases[1]), Find(headers, Aliases[2]), Find(headers, Aliases[3]), Find(headers, Aliases[4]));
    }

    private static int Find(string[] headers, IEnumerable<string> aliases)
    {
        var set = aliases.Select(Normalize).ToHashSet(StringComparer.Ordinal);
        return Array.FindIndex(headers, set.Contains);
    }
    private static string Normalize(string value)
        => Regex.Replace(value.Trim().TrimStart('\uFEFF').ToLowerInvariant().Replace('ё', 'е'), @"[\s_\-]+", " ").Trim();
}
