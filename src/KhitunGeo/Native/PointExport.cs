using System.Globalization;
using System.IO;
using System.Numerics;
using System.Text;

namespace KhitunGeo.Native;

internal enum PointExportFormat { AutoCad, AutoCadScript, ExcelXlsx, Pnezd, Penzd, Nez, Enz, Xyz, Blh }
internal sealed record PointExportOptions(PointExportFormat Format, string Delimiter, int Decimals, bool Header);
internal sealed record PointExportResult(string[] Columns, IReadOnlyList<string[]> Rows, string Text);

internal static class PointExport
{
    public static PointExportResult Create(IReadOnlyList<SurveyPoint> points, PointExportOptions options, bool geographic)
    {
        if (options.Decimals is < 0 or > 9) throw new ArgumentException("Точность должна быть от 0 до 9.");
        if (options.Delimiter is not ("," or ";" or "\t" or " ")) throw new ArgumentException("Недопустимый разделитель.");
        if (options.Format == PointExportFormat.Blh && !geographic)
            throw new ArgumentException("B,L,H требует географических координат. Сначала выполните пересчёт.");
        // CAD export writes the table's numeric coordinates without conversion.
        // The source picker describes the next calculation, not necessarily this data.
        if (options.Format == PointExportFormat.AutoCadScript)
            return CreateAutoCadScript(points, options.Decimals);
        string[] columns = options.Format switch
        {
            PointExportFormat.Pnezd => new[] { "P", "N", "E", "Z", "D" },
            PointExportFormat.Penzd => new[] { "P", "E", "N", "Z", "D" },
            PointExportFormat.ExcelXlsx => new[] { "№ точки", "X", "Y", "Z/H", "Описание" },
            PointExportFormat.Nez => new[] { "N", "E", "Z" },
            PointExportFormat.Enz => new[] { "E", "N", "Z" },
            PointExportFormat.AutoCad or PointExportFormat.Xyz => new[] { "X", "Y", "Z" },
            PointExportFormat.Blh => new[] { "B", "L", "H" },
            _ => throw new ArgumentException("Неизвестный формат экспорта.")
        };
        var rows = new List<string[]>();
        for (var index = 0; index < points.Count; index++)
        {
            var p = points[index];
            PointWorkspace.ValidatePoint(p);
            if (p.X is null && p.Y is null) continue;
            if (p.X is null || p.Y is null) throw new ArgumentException($"Точка {p.Name}: заполните обе координаты.");
            var name = Clean(string.IsNullOrEmpty(p.Name) ? (index + 1).ToString(CultureInfo.InvariantCulture) : p.Name, options.Delimiter);
            var n = Fixed(p.X.Value, options.Decimals); var e = Fixed(p.Y.Value, options.Decimals);
            // Preserve the existing export convention: absent height is exported as zero, without changing the table.
            var z = Fixed(p.Height ?? 0, options.Decimals);
            rows.Add(options.Format switch
            {
                PointExportFormat.Pnezd => new[] { name, n, e, z, Clean(p.Description, options.Delimiter) },
                PointExportFormat.Penzd => new[] { name, e, n, z, Clean(p.Description, options.Delimiter) },
                PointExportFormat.ExcelXlsx => new[] { name, n, e, p.Height is double height ? Fixed(height, options.Decimals) : "", p.Description },
                PointExportFormat.Enz or PointExportFormat.AutoCad => new[] { e, n, z },
                _ => new[] { n, e, z }
            });
        }
        if (rows.Count == 0) throw new ArgumentException("Нет заполненных точек для экспорта.");
        var text = new StringBuilder();
        if (options.Header) text.AppendJoin(options.Delimiter, columns).Append("\r\n");
        foreach (var row in rows) text.AppendJoin(options.Delimiter, row).Append("\r\n");
        return new(columns, rows, text.ToString());
    }

    public static void Save(string path, IReadOnlyList<SurveyPoint> points, PointExportOptions options, bool geographic, string coordinateSystem = "")
    {
        var result = Create(points, options, geographic);
        if (options.Format == PointExportFormat.ExcelXlsx)
        {
            ExcelXlsxWriter.Save(path, result, coordinateSystem);
            return;
        }
        var fullPath = Path.GetFullPath(path);
        var temporary = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, result.Text, new UTF8Encoding(options.Format != PointExportFormat.AutoCadScript));
            File.Move(temporary, fullPath, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private static PointExportResult CreateAutoCadScript(IReadOnlyList<SurveyPoint> points, int decimals)
    {
        var columns = new[] { "Команда", "X (восток)", "Y (север)", "Z" };
        var rows = new List<string[]>();
        var script = new StringBuilder("_.UCS\r\n_World\r\n");
        for (var index = 0; index < points.Count; index++)
        {
            var point = points[index];
            PointWorkspace.ValidatePoint(point);
            if (point.X is null && point.Y is null) continue;
            if (point.X is null || point.Y is null)
                throw new ArgumentException($"Точка {point.Name}: заполните обе координаты.");
            var x = Fixed(point.Y.Value, decimals);
            var y = Fixed(point.X.Value, decimals);
            var z = Fixed(point.Height ?? 0, decimals);
            rows.Add(new[] { "POINT", x, y, z });
            script.Append("_.POINT\r\n").Append(x).Append(',').Append(y).Append(',').Append(z).Append("\r\n");
        }
        if (rows.Count == 0) throw new ArgumentException("Нет заполненных точек для экспорта.");
        script.Append("_.UCS\r\n_Previous\r\n\r\n");
        return new(columns, rows, script.ToString());
    }

    private static string Clean(string value, string delimiter)
    {
        var result = System.Text.RegularExpressions.Regex.Replace(value, "[\\r\\n\\t]+", " ").Trim();
        return result.Replace(delimiter, " ");
    }

    // Round the exact binary double, matching JS toFixed's half-away-from-zero rule.
    // Decimal conversion first would change values such as 1.005; default F uses ties-to-even.
    private static string Fixed(double value, int decimals)
    {
        var bits = BitConverter.DoubleToInt64Bits(Math.Abs(value));
        var exponent = (int)((bits >> 52) & 0x7ff);
        var mantissa = bits & 0x000fffffffffffffL;
        if (exponent != 0) mantissa |= 1L << 52;
        var shift = (exponent == 0 ? -1022 : exponent - 1023) - 52;
        BigInteger scaled = new BigInteger(mantissa) * BigInteger.Pow(10, decimals);
        if (shift >= 0) scaled <<= shift;
        else
        {
            var denominator = BigInteger.One << -shift;
            scaled = BigInteger.DivRem(scaled, denominator, out var remainder);
            if (remainder * 2 >= denominator) scaled++;
        }
        var digits = scaled.ToString(CultureInfo.InvariantCulture).PadLeft(decimals + 1, '0');
        if (decimals > 0) digits = digits.Insert(digits.Length - decimals, ".");
        return value < 0 ? "-" + digits : digits;
    }
}
