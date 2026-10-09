using System.IO;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace KhitunGeo.Native;

internal sealed record QualityIssue(int Row, string Severity, string Message);

internal sealed class ConversionPassport
{
    public const string HashFormat = "SHA-256 / native-v1 / UTF-8 JSON";
    public DateTimeOffset StartedAt { get; }
    public string SourceName { get; }
    public string TargetName { get; }
    public string Settings { get; }
    public string AppVersion { get; }
    public int Count { get; }
    public string BeforeHash { get; }
    public string AfterHash { get; }
    public IReadOnlyList<int> Zones { get; }
    public int IssueCount { get; }
    public IReadOnlyList<QualityIssue> Issues { get; }

    internal ConversionPassport(PassportCapture capture, IReadOnlyList<SurveyPoint> result,
        IReadOnlyList<int> zones, IReadOnlyList<QualityIssue> issues)
    {
        StartedAt = capture.StartedAt; SourceName = capture.SourceName; TargetName = capture.TargetName;
        Settings = capture.Settings; AppVersion = capture.AppVersion; BeforeHash = capture.BeforeHash;
        Count = result.Count; AfterHash = Fingerprint(result);
        Zones = Array.AsReadOnly(zones.ToArray());
        IssueCount = issues.Count; Issues = Array.AsReadOnly(issues.Take(500).ToArray());
    }

    public bool Matches(IReadOnlyList<SurveyPoint> points) => Fingerprint(points) == AfterHash;

    public static string Fingerprint(IReadOnlyList<SurveyPoint> points)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            writer.WriteStartArray();
            foreach (var p in points)
            {
                PointWorkspace.ValidatePoint(p);
                if (p.Name is null || p.Description is null) throw new ArgumentException("Не заданы текстовые поля точки.");
                writer.WriteStartArray(); writer.WriteStringValue(p.Name);
                WriteNumber(writer, p.X); WriteNumber(writer, p.Y); WriteNumber(writer, p.Height);
                writer.WriteStringValue(p.Description); writer.WriteEndArray();
            }
            writer.WriteEndArray();
        }
        return Convert.ToHexString(SHA256.HashData(stream.GetBuffer().AsSpan(0, (int)stream.Length)));
    }

    private static void WriteNumber(Utf8JsonWriter writer, double? value)
    { if (value.HasValue) writer.WriteNumberValue(value.Value); else writer.WriteNullValue(); }
}

internal sealed class PassportCapture
{
    private readonly SurveyPoint[] points;
    internal DateTimeOffset StartedAt { get; }
    internal string SourceName { get; }
    internal string TargetName { get; }
    internal string Settings { get; }
    internal string AppVersion { get; }
    internal string BeforeHash { get; }

    private PassportCapture(IReadOnlyList<SurveyPoint> points, string sourceName, string targetName,
        string settings, string appVersion, DateTimeOffset startedAt)
    {
        this.points = points.ToArray();
        BeforeHash = ConversionPassport.Fingerprint(this.points);
        if (string.IsNullOrWhiteSpace(sourceName) || string.IsNullOrWhiteSpace(targetName) || string.IsNullOrWhiteSpace(appVersion))
            throw new ArgumentException("Не заданы сведения о преобразовании.");
        using var document = JsonDocument.Parse(settings);
        var root = document.RootElement;
        if (root.GetProperty("version").GetInt32() != 1 || root.GetProperty("source").ValueKind != JsonValueKind.Object
            || root.GetProperty("target").ValueKind != JsonValueKind.Object || root.GetProperty("zone").ValueKind != JsonValueKind.Object)
            throw new ArgumentException("Некорректные настройки паспорта.");
        SourceName = sourceName; TargetName = targetName; Settings = settings; AppVersion = appVersion; StartedAt = startedAt.ToUniversalTime();
    }

    public static PassportCapture Capture(IReadOnlyList<SurveyPoint> points, string sourceName, string targetName,
        string settings, string appVersion, DateTimeOffset startedAt) => new(points, sourceName, targetName, settings, appVersion, startedAt);

    public ConversionPassport Complete(IReadOnlyList<SurveyPoint> result, IReadOnlyList<int> zones, IReadOnlyList<QualityIssue> issues)
    {
        if (result.Count != points.Length) throw new ArgumentException("Изменилось количество точек.");
        for (var i = 0; i < result.Count; i++)
        {
            PointWorkspace.ValidatePoint(result[i]);
            if (result[i].X is null || result[i].Y is null || result[i].Name != points[i].Name
                || result[i].Description != points[i].Description || result[i].Height != points[i].Height)
                throw new ArgumentException("Результат паспорта не соответствует исходным точкам.");
        }
        if (zones.Any(z => z < 1 || z > 120)) throw new ArgumentException("Некорректная зона.");
        foreach (var issue in issues)
            if (issue.Row < 0 || issue.Row >= points.Length || issue.Severity != "warning" || string.IsNullOrWhiteSpace(issue.Message))
                throw new ArgumentException("Нельзя завершить паспорт с ошибкой или некорректным замечанием.");
        return new(this, result, zones, issues);
    }
}
