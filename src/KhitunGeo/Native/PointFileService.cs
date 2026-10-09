using System.Globalization;
using System.IO;
using System.Text;

namespace KhitunGeo.Native;

internal static class PointFileService
{
    private const long MaxTextBytes = 16 * 1024 * 1024;

    public static IReadOnlyList<SurveyPoint> Read(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        if (extension is not (".csv" or ".tsv" or ".txt"))
            throw new FormatException("Поддерживаются CSV, TSV и TXT.");
        if (new FileInfo(path).Length > MaxTextBytes)
            throw new FormatException("Табличный файл больше 16 МБ. Разделите файл.");
        return TabularPaste.Parse(File.ReadAllText(path, new UTF8Encoding(false, true)));
    }

    public static string ToCsv(IReadOnlyList<SurveyPoint> points)
    {
        var csv = new StringBuilder("Name;X;Y;Z;Description\r\n");
        foreach (var point in points)
        {
            PointWorkspace.ValidatePoint(point);
            csv.AppendJoin(';', new[] { Cell(point.Name), Cell(Number(point.X)), Cell(Number(point.Y)), Cell(Number(point.Height)), Cell(point.Description) });
            csv.Append("\r\n");
        }
        return csv.ToString();
    }

    public static void SaveCsv(string path, IReadOnlyList<SurveyPoint> points)
    {
        var text = ToCsv(points);
        var fullPath = Path.GetFullPath(path);
        var temporary = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, text, new UTF8Encoding(true));
            File.Move(temporary, fullPath, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private static string Number(double? value) => value?.ToString("R", CultureInfo.InvariantCulture) ?? "";
    private static string Cell(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
}
