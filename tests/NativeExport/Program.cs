using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Xml.Linq;
using KhitunGeo.Native;

static void Equal(string expected, string actual)
{ if (expected != actual) throw new Exception($"Expected [{expected}], got [{actual}]"); }
static void Reject(Action action)
{ try { action(); } catch (ArgumentException) { return; } throw new Exception("Expected validation failure"); }
var points = new[] { new SurveyPoint("Т1", 123.125, 456.875, -7.25, "дно"), new SurveyPoint("", 11, 22, null, "") };
var geo = new[] { new SurveyPoint("g", 56.123456789, 92.987654321, -1, "") };
CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ru-RU");
foreach (var (format, expected) in new[] {
    (PointExportFormat.Pnezd, "Т1,123.125,456.875,-7.250,дно"),
    (PointExportFormat.Penzd, "Т1,456.875,123.125,-7.250,дно"),
    (PointExportFormat.Nez, "123.125,456.875,-7.250"),
    (PointExportFormat.Enz, "456.875,123.125,-7.250"),
    (PointExportFormat.AutoCad, "456.875,123.125,-7.250"),
    (PointExportFormat.Xyz, "123.125,456.875,-7.250") })
{
    var result = PointExport.Create(points, new(format, ",", 3, false), false);
    Equal(expected, string.Join(",", result.Rows[0]));
    if (result.Rows.Count != 2) throw new Exception("Export must include all points");
}
var script = PointExport.Create(points, new(PointExportFormat.AutoCadScript, ",", 3, true), false);
Equal("_.UCS\r\n_World\r\n_.POINT\r\n456.875,123.125,-7.250\r\n_.POINT\r\n22.000,11.000,0.000\r\n_.UCS\r\n_Previous\r\n\r\n", script.Text);
if (script.Rows.Count != points.Length || script.Columns.Length != 4) throw new Exception("AutoCAD script must contain every point and expose coordinates in the preview");
var excelPreview = PointExport.Create(points, new(PointExportFormat.ExcelXlsx, ",", 9, true), false);
if (excelPreview.Rows.Count != 2 || excelPreview.Columns.Length != 5 || excelPreview.Rows[1][3] != "")
    throw new Exception("Excel export preview must preserve all points and missing Z");
if (PointExport.Create(geo, new(PointExportFormat.ExcelXlsx, ",", 9, false), true).Rows.Count != 1)
    throw new Exception("Excel export must accept geographic coordinates");
Equal("2,11.000,22.000,0.000,", string.Join(",", PointExport.Create(points, new(PointExportFormat.Pnezd, ",", 3, false), false).Rows[1]));
Equal("X\tY\tZ\r\n456.9\t123.1\t-7.3\r\n22.0\t11.0\t0.0\r\n", PointExport.Create(points, new(PointExportFormat.AutoCad, "\t", 1, true), false).Text);
Equal("123.125;456.875;-7.250\r\n11.000;22.000;0.000\r\n", PointExport.Create(points, new(PointExportFormat.Xyz, ";", 3, false), false).Text);
Equal("123.125 456.875 -7.250\r\n11.000 22.000 0.000\r\n", PointExport.Create(points, new(PointExportFormat.Xyz, " ", 3, false), false).Text);
Equal("B,L,H\r\n56.123456789,92.987654321,-1.000000000\r\n", PointExport.Create(geo, new(PointExportFormat.Blh, ",", 9, true), true).Text);
Reject(() => PointExport.Create(points, new(PointExportFormat.Blh, ",", 3, false), false));
Reject(() => PointExport.Create(geo, new(PointExportFormat.Nez, ",", 3, false), true));
Reject(() => PointExport.Create(points, new(PointExportFormat.Nez, ",", 10, false), false));
Reject(() => PointExport.Create(points, new((PointExportFormat)99, ",", 3, false), false));
Reject(() => PointExport.Create(points, new(PointExportFormat.Nez, "|", 3, false), false));
Reject(() => PointExport.Create(new[] { points[0] with { Y = null } }, new(PointExportFormat.Nez, ",", 3, false), false));
Reject(() => PointExport.Create(new[] { points[0] with { X = double.NaN } }, new(PointExportFormat.Nez, ",", 3, false), false));
Reject(() => PointExport.Create(Array.Empty<SurveyPoint>(), new(PointExportFormat.Nez, ",", 3, false), false));
var empty = new SurveyPoint("blank", null, null, null, "");
if (PointExport.Create(new[] { empty, points[0] }, new(PointExportFormat.Nez, ",", 3, false), false).Rows.Count != 1) throw new Exception("Blank coordinate rows must be skipped");
Equal("a b,123.125,456.875,-7.250,one two three four", string.Join(",", PointExport.Create(new[] { points[0] with { Name = "a,b", Description = "one\ntwo\tthree,four" } }, new(PointExportFormat.Pnezd, ",", 3, false), false).Rows[0]));
Equal("3,-3,0\r\n", PointExport.Create(new[] { new SurveyPoint("r", 2.5, -2.5, -0.0, "") }, new(PointExportFormat.Xyz, ",", 0, false), false).Text);
Equal("1.00,-1.00,0.00\r\n", PointExport.Create(new[] { new SurveyPoint("r", 1.005, -1.005, 0, "") }, new(PointExportFormat.Xyz, ",", 2, false), false).Text);
var directory = Path.Combine(Path.GetTempPath(), "khitun-export-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(directory);
try
{
    var path = Path.Combine(directory, "points.csv"); File.WriteAllText(path, "original");
    Reject(() => PointExport.Save(path, new[] { points[0] with { X = null } }, new(PointExportFormat.Nez, ",", 3, false), false));
    Equal("original", File.ReadAllText(path));
    PointExport.Save(path, points, new(PointExportFormat.Xyz, ",", 3, false), false);
    Equal("123.125,456.875,-7.250\r\n11.000,22.000,0.000\r\n", File.ReadAllText(path));
    var scriptPath = Path.Combine(directory, "points.scr");
    PointExport.Save(scriptPath, points, new(PointExportFormat.AutoCadScript, ",", 3, false), false);
    Equal("_.UCS\r\n_World\r\n_.POINT\r\n456.875,123.125,-7.250\r\n_.POINT\r\n22.000,11.000,0.000\r\n_.UCS\r\n_Previous\r\n\r\n", File.ReadAllText(scriptPath));
    var xlsxPath = Path.Combine(directory, "points.xlsx");
    PointExport.Save(xlsxPath, points, new(PointExportFormat.ExcelXlsx, ",", 9, false), false, "МСК-164");
    using (var archive = ZipFile.OpenRead(xlsxPath))
    {
        var worksheetEntry = archive.GetEntry("xl/worksheets/sheet1.xml") ?? throw new Exception("Missing worksheet");
        using var stream = worksheetEntry.Open();
        var sheet = XDocument.Load(stream);
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        string Cell(string address) => sheet.Descendants(ns + "c").Single(c => (string?)c.Attribute("r") == address).Value;
        string CellOrEmpty(string address) => sheet.Descendants(ns + "c").SingleOrDefault(c => (string?)c.Attribute("r") == address)?.Value ?? "";
        if (Cell("B4") != "123.125000000" || Cell("C4") != "456.875000000" || Cell("D4") != "-7.250000000" || CellOrEmpty("D5") != "")
            throw new Exception($"XLSX cell mismatch: B4={Cell("B4")}, C4={Cell("C4")}, D4={Cell("D4")}, D5={CellOrEmpty("D5")}");
        if (Cell("A4") != "Т1" || Cell("E4") != "дно" || Cell("B1") != "МСК-164")
            throw new Exception("XLSX must record point name, description and current coordinate system");
        if (sheet.Descendants(ns + "c").Single(c => (string?)c.Attribute("r") == "B4").Attribute("t") is not null)
            throw new Exception("XLSX coordinate cells must be numeric, not strings");
        var workbookEntry = archive.GetEntry("xl/workbook.xml") ?? throw new Exception("Missing workbook");
        using var workbookStream = workbookEntry.Open();
        if (!XDocument.Load(workbookStream).Descendants(ns + "sheet").Any(s => (string?)s.Attribute("name") == "Точки"))
            throw new Exception("Missing points worksheet");
    }
    if (Directory.GetFiles(directory).Length != 3) throw new Exception("Temporary export file leaked");
    if (points[0].X != 123.125 || points[1].Height != null) throw new Exception("Export mutated points");
}
finally { Directory.Delete(directory, true); }
Console.WriteLine("PASS native export: 9 formats including AutoCAD SCR and XLSX, axes, precision, culture, validation and atomic saving");
