using System.Globalization;
using System.IO;
using Microsoft.VisualBasic.FileIO;

namespace KhitunGeo.Native;

internal static class TabularPaste
{
    public static IReadOnlyList<SurveyPoint> Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return Array.Empty<SurveyPoint>();
        var separator = DetectDelimiter(text);
        using var parser = new TextFieldParser(new StringReader(text));
        parser.SetDelimiters(separator);
        parser.HasFieldsEnclosedInQuotes = true;
        parser.TrimWhiteSpace = false;
        var result = new List<SurveyPoint>();
        while (!parser.EndOfData)
        {
            string[] fields;
            try { fields = parser.ReadFields() ?? Array.Empty<string>(); }
            catch (MalformedLineException ex) { throw new FormatException("Invalid quoted row.", ex); }
            if (result.Count == 0 && fields.Length >= 4 &&
                fields[1].Trim().Equals("X", StringComparison.OrdinalIgnoreCase) &&
                fields[2].Trim().Equals("Y", StringComparison.OrdinalIgnoreCase)) continue;
            if (fields.Length is < 4 or > 5) throw new FormatException("Expected name, X, Y, height and optional description.");
            result.Add(new SurveyPoint(fields[0], Number(fields[1]), Number(fields[2]), Number(fields[3]), fields.Length == 5 ? fields[4] : ""));
        }
        return result;
    }

    public static double? Number(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        if (!double.TryParse(text.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || !double.IsFinite(value))
            throw new FormatException("Invalid coordinate: " + text);
        return value;
    }

    private static string DetectDelimiter(string text)
    {
        // Inspect parsed fields so punctuation inside quotes cannot choose the delimiter.
        foreach (var delimiter in new[] { "\t", ";", "," })
        {
            using var probe = new TextFieldParser(new StringReader(text));
            probe.SetDelimiters(delimiter);
            probe.HasFieldsEnclosedInQuotes = true;
            try
            {
                if (probe.ReadFields()?.Length is >= 4 and <= 5) return delimiter;
            }
            catch (MalformedLineException) { }
        }
        throw new FormatException("Expected name, X, Y, height and optional description.");
    }
}
