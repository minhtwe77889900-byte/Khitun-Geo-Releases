using System.Globalization;
using System.IO;
using System.Text;

namespace KhitunGeo.Native;

internal static class PointFileService
{
    public static IReadOnlyList<SurveyPoint> Read(string path)
    {
        var preview = PointImportService.Open(path);
        var review = PointImportService.Map(preview, PointImportService.DetectMapping(preview), preview.HasHeader, swapXY: false);
        if (review.Errors.Count > 0) throw new FormatException(string.Join(Environment.NewLine, review.Errors.Take(3)));
        return review.Points;
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
