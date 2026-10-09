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

    public static IReadOnlyList<SurveyPoint> Read(string path)
    {
        using var archive = ZipFile.OpenRead(path);
        var sharedStrings = ReadSharedStrings(archive);
        var sheetPath = FindFirstSheet(archive);
        var sheet = ReadXml(archive, sheetPath);
        var text = new StringBuilder();
        foreach (var row in sheet.Descendants(Main + "row"))
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
            text.AppendLine(string.Join("\t", fields.Select(TsvCell)));
        }
        return TabularPaste.Parse(text.ToString());
    }

    private static string FindFirstSheet(ZipArchive archive)
    {
        var workbook = archive.GetEntry("xl/workbook.xml");
        var rels = archive.GetEntry("xl/_rels/workbook.xml.rels");
        if (workbook is null || rels is null)
            return archive.Entries.FirstOrDefault(e => e.FullName.StartsWith("xl/worksheets/sheet", StringComparison.OrdinalIgnoreCase) && e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))?.FullName
                ?? throw new FormatException("В книге XLSX не найден лист с данными.");

        var workbookXml = LoadXml(workbook);
        var firstSheet = workbookXml.Descendants(Main + "sheet").FirstOrDefault()
            ?? throw new FormatException("В книге XLSX нет листов.");
        var relationshipId = (string?)firstSheet.Attribute(OfficeRel + "id");
        var relationship = LoadXml(rels).Root?.Elements(PackageRel + "Relationship")
            .FirstOrDefault(e => (string?)e.Attribute("Id") == relationshipId);
        var target = (string?)relationship?.Attribute("Target");
        if (string.IsNullOrWhiteSpace(target)) throw new FormatException("Не найден первый лист книги XLSX.");
        var segments = new List<string>();
        foreach (var segment in target.Replace('\\', '/').Split('/'))
        {
            if (segment is "" or ".") continue;
            if (segment == "..") { if (segments.Count > 0) segments.RemoveAt(segments.Count - 1); continue; }
            segments.Add(segment);
        }
        var normalized = target.StartsWith('/') ? string.Join('/', segments) : "xl/" + string.Join('/', segments);
        if (archive.GetEntry(normalized) is null) throw new FormatException("Лист книги XLSX повреждён или отсутствует.");
        return normalized;
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
