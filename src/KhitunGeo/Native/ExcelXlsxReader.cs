using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace KhitunGeo.Native;

internal static class ExcelXlsxReader
{
    private static readonly XNamespace Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace OfficeRel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace PackageRel = "http://schemas.openxmlformats.org/package/2006/relationships";
    private const long MaxEntryBytes = 32 * 1024 * 1024;

    private sealed record SheetReference(string Name, string Path);

    public static IReadOnlyList<SurveyPoint> Read(string path)
    {
        var rows = ReadSheetRows(path, null);
        var text = string.Join("\r\n", rows.Select(row => string.Join("\t", row.Select(TsvCell))));
        return TabularPaste.Parse(text);
    }

    public static IReadOnlyList<string> GetSheetNames(string path)
    {
        using var archive = ZipFile.OpenRead(path);
        return FindSheets(archive).Select(sheet => sheet.Name).ToArray();
    }

    public static string[][] ReadSheetRows(string path, string? sheetName)
    {
        using var archive = ZipFile.OpenRead(path);
        var sharedStrings = ReadSharedStrings(archive);
        var sheets = FindSheets(archive);
        var selectedSheet = sheets.FirstOrDefault(s => sheetName is not null && string.Equals(s.Name, sheetName, StringComparison.Ordinal)) ?? sheets.FirstOrDefault();
        var sheetPath = selectedSheet?.Path ?? throw new FormatException("В книге XLSX не найден лист с данными.");
        var sheetXml = ReadXml(archive, sheetPath);
        var rows = new List<string[]>();
        foreach (var row in sheetXml.Descendants(Main + "row"))
        {
            var cells = new SortedDictionary<int, string>();
            foreach (var cell in row.Elements(Main + "c"))
            {
                var reference = (string?)cell.Attribute("r") ?? "A1";
                var column = ColumnNumber(reference);
                if (column >= 16_384) throw new FormatException("В книге XLSX превышено число столбцов листа.");
                var type = (string?)cell.Attribute("t");
                var value = type switch
                {
                    "s" => SharedValue(sharedStrings, (string?)cell.Element(Main + "v")),
                    "inlineStr" => string.Concat(cell.Descendants(Main + "t").Select(t => t.Value)),
                    "b" => (string?)cell.Element(Main + "v") == "1" ? "TRUE" : "FALSE",
                    _ => (string?)cell.Element(Main + "v") ?? string.Concat(cell.Descendants(Main + "t").Select(t => t.Value))
                };
                cells[column] = value;
            }
            if (cells.Count == 0) continue;
            var fields = new string[cells.Keys.Max() + 1];
            Array.Fill(fields, "");
            foreach (var (column, value) in cells) fields[column] = value;
            rows.Add(fields);
        }
        return rows.ToArray();
    }

    private static IReadOnlyList<SheetReference> FindSheets(ZipArchive archive)
    {
        var workbook = archive.GetEntry("xl/workbook.xml");
        var rels = archive.GetEntry("xl/_rels/workbook.xml.rels");
        if (workbook is null || rels is null)
            return archive.Entries.Where(e => e.FullName.StartsWith("xl/worksheets/sheet", StringComparison.OrdinalIgnoreCase) && e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                .OrderBy(e => e.FullName, StringComparer.OrdinalIgnoreCase)
                .Select((entry, index) => new SheetReference($"Лист {index + 1}", entry.FullName)).ToArray();

        var workbookXml = LoadXml(workbook);
        var relationships = LoadXml(rels).Root?.Elements(PackageRel + "Relationship").ToDictionary(
            e => (string?)e.Attribute("Id") ?? "", e => (string?)e.Attribute("Target") ?? "", StringComparer.Ordinal)
            ?? new Dictionary<string, string>();
        var result = new List<SheetReference>();
        foreach (var sheet in workbookXml.Descendants(Main + "sheet"))
        {
            var id = (string?)sheet.Attribute(OfficeRel + "id") ?? "";
            if (!relationships.TryGetValue(id, out var target) || string.IsNullOrWhiteSpace(target)) continue;
            var segments = target.StartsWith('/') ? new List<string>() : new List<string> { "xl" };
            foreach (var segment in target.Replace('\\', '/').Split('/'))
            {
                if (segment is "" or ".") continue;
                if (segment == "..") { if (segments.Count > 0) segments.RemoveAt(segments.Count - 1); continue; }
                segments.Add(segment);
            }
            var normalized = string.Join('/', segments);
            if (archive.GetEntry(normalized) is not null) result.Add(new SheetReference((string?)sheet.Attribute("name") ?? $"Лист {result.Count + 1}", normalized));
        }
        if (result.Count == 0) throw new FormatException("Не найден ни один лист с данными в книге XLSX.");
        return result;
    }

    private static List<string> ReadSharedStrings(ZipArchive archive)
    {
        var entry = archive.GetEntry("xl/sharedStrings.xml");
        if (entry is null) return new();
        return LoadXml(entry).Descendants(Main + "si")
            .Select(si => string.Concat(si.Descendants(Main + "t").Select(t => t.Value))).ToList();
    }

    private static string SharedValue(IReadOnlyList<string> strings, string? raw)
    {
        if (!int.TryParse(raw, out var index) || index < 0 || index >= strings.Count)
            throw new FormatException("В книге XLSX повреждён индекс текстового значения.");
        return strings[index];
    }

    private static XDocument ReadXml(ZipArchive archive, string path) => LoadXml(archive.GetEntry(path)
        ?? throw new FormatException("Не найден лист книги XLSX."));

    private static XDocument LoadXml(ZipArchiveEntry entry)
    {
        if (entry.Length > MaxEntryBytes) throw new FormatException("Лист книги Excel слишком большой.");
        using var stream = entry.Open();
        return XDocument.Load(stream, LoadOptions.None);
    }

    private static int ColumnNumber(string reference)
    {
        var column = 0;
        foreach (var c in reference)
        {
            if (!char.IsAsciiLetter(c)) break;
            column = checked(column * 26 + char.ToUpperInvariant(c) - 'A' + 1);
        }
        return Math.Max(0, column - 1);
    }

    private static string TsvCell(string value) => value.IndexOfAny(new[] { '\t', '\r', '\n', '"' }) >= 0
        ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
}
