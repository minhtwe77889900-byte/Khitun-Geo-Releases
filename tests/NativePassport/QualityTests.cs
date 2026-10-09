using System.IO;
using System.Text.Json;
using System.Threading;
using KhitunGeo.Native;

internal static class QualityTests
{
    internal static void Run()
    {
        var runtime = new NativeQualityRuntime(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "workspace-core.js")));
        var points = new[] {
            new SurveyPoint("a",56,92,null,"quote\"; throw Error('injected')"),
            new SurveyPoint("a",56,92,-12,""), new SurveyPoint("",91,181,0,""),
            new SurveyPoint("bad",12,null,0,""), new SurveyPoint("empty",null,null,null,"")
        };
        var issues = runtime.Check(points, true, CancellationToken.None);
        ModelTests.Assert(issues.Count == 6, "Quality expected six issues");
        ModelTests.Assert(issues[0] == new QualityIssue(0,"warning","Z не задана; в горизонтальном расчёте используется 0"), "Null height warning");
        ModelTests.Assert(issues[1] == new QualityIssue(1,"warning","Повтор номера: строка 1"), "Duplicate ID");
        ModelTests.Assert(issues[2] == new QualityIssue(1,"warning","Совпадающие X/Y: строка 1"), "Duplicate coordinates");
        ModelTests.Assert(issues[3].Row == 2 && issues[3].Severity == "error" && issues[4].Message == "Нет номера точки", "Geographic bounds and empty number");
        ModelTests.Assert(issues[5] == new QualityIssue(3,"error","Некорректные или неполные X/Y"), "Incomplete coordinates");
        ModelTests.Reject(() => NativeQualityRuntime.EnsureNoErrors(issues));
        var valid = new[] { new SurveyPoint("edge",90,-180,-2,"") };
        ModelTests.Assert(runtime.Check(valid, true, CancellationToken.None).Count == 0, "Geographic boundary");
        var many = Enumerable.Range(0,700).Select(i => new SurveyPoint(i.ToString(),i+1,i+2,null,"")).ToArray();
        var warnings = runtime.Check(many, false, CancellationToken.None);
        ModelTests.Assert(warnings.Count == 700 && warnings.All(i => i.Severity == "warning"), "700 absent height warnings");
        NativeQualityRuntime.EnsureNoErrors(warnings);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        try { runtime.Check(valid,true,cancellation.Token); throw new Exception("Cancellation ignored"); } catch (OperationCanceledException) { }
        try
        {
            var hostile = new NativeQualityRuntime("while(true){}");
            using var deadline = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
            hostile.Check(valid, true, deadline.Token); throw new Exception("Host cancellation ignored");
        }
        catch (OperationCanceledException) { }
        var output = Environment.GetEnvironmentVariable("KHITUN_QUALITY_PARITY_FILE");
        if (output is not null) File.WriteAllText(output, JsonSerializer.Serialize(new { points = points.Select(p => new { no=p.Name,x=p.X,y=p.Y,z=p.Height,d=p.Description }), geographic=true, issues=issues.Select(i=>new {row=i.Row,severity=i.Severity,message=i.Message}) }));
        Console.WriteLine("PASS real Jint quality: warnings/errors, metadata, zones, cancellation and 700 issues");
    }
}
