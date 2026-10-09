using System.Text.Json;
using System.Threading;
using Jint;

namespace KhitunGeo.Native;

internal sealed class NativeQualityRuntime(string workspaceCore)
{
    public IReadOnlyList<QualityIssue> Check(IReadOnlyList<SurveyPoint> points, bool geographic, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        foreach (var p in points) PointWorkspace.ValidatePoint(p);
        var json = JsonSerializer.Serialize(points.Select(p => new { no=p.Name, x=p.X, y=p.Y, z=p.Height, d=p.Description }));
        if (json.Length > 8_000_000) throw new ArgumentException("Слишком большой запрос проверки.");
        try
        {
            var engine = new Engine(o => o.LimitMemory(128_000_000).MaxStatements(100_000_000)
                .TimeoutInterval(TimeSpan.FromSeconds(30)).CancellationToken(cancellationToken));
            engine.Execute(workspaceCore);
            engine.Execute("function khitunQualityJson(points, geographic) { return JSON.stringify(KhitunWorkspace.quality(JSON.parse(points), geographic)); }");
            var result = engine.Invoke("khitunQualityJson", json, geographic).AsString();
            cancellationToken.ThrowIfCancellationRequested();
            using var document = JsonDocument.Parse(result);
            var issues = new List<QualityIssue>();
            foreach (var row in document.RootElement.EnumerateArray())
            {
                var index = row.GetProperty("row").GetInt32();
                var severity = row.GetProperty("severity").GetString();
                var message = row.GetProperty("message").GetString();
                if (index < 0 || index >= points.Count || severity is not ("warning" or "error") || string.IsNullOrWhiteSpace(message))
                    throw new FormatException("Некорректный ответ проверки качества.");
                issues.Add(new(index, severity, message));
            }
            return issues.AsReadOnly();
        }
        catch (Jint.Runtime.ExecutionCanceledException) when (cancellationToken.IsCancellationRequested)
        { throw new OperationCanceledException(cancellationToken); }
    }

    public static void EnsureNoErrors(IReadOnlyList<QualityIssue> issues)
    {
        var errors = issues.Where(i => i.Severity == "error").ToArray();
        if (errors.Length > 0) throw new ArgumentException("Ошибки исходных данных:\n" + string.Join("\n", errors.Take(10).Select(i => $"Строка {i.Row + 1}: {i.Message}")));
    }
}
