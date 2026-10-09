using System.Text.Json;
using KhitunGeo.Native;

internal static class PassportWorkflowTests
{
    internal static void Run()
    {
        var original = new[] { new SurveyPoint("001",65,84,-12,"Глубина"), new SurveyPoint("002",65.01,84.02,null,"") };
        using var geo = JsonDocument.Parse("{\"kind\":\"geo\",\"datum\":\"wgs\"}");
        using var target = JsonDocument.Parse("{\"kind\":\"gk6\",\"datum\":\"sk42\"}");
        var settings = CoordinateBridge.BuildRequest(Array.Empty<SurveyPoint>(),geo.RootElement,target.RootElement,new(true,28,true,true));
        const string response = "{\"version\":1,\"targetZones\":[15],\"points\":[{\"name\":\"001\",\"x\":7000000,\"y\":15450000,\"height\":-12,\"description\":\"Глубина\"},{\"name\":\"002\",\"x\":7000100,\"y\":15450100,\"height\":null,\"description\":\"\"}]}";
        var workspace = new PointWorkspace(); workspace.ReplacePoints(original);
        var state = new PassportState(); var interpretation = settings;
        PassportConversion Operation() => new(workspace.Points,settings,"Исходная","Целевая","1.7.5",DateTimeOffset.UtcNow);
        var operation = Operation();
        try { operation.Apply(workspace,settings,response.Replace("7000100","null"),Array.Empty<QualityIssue>(),state); throw new Exception("Bad result accepted"); } catch (Exception ex) when (ex is FormatException or InvalidOperationException) { }
        ModelTests.Assert(state.Current is null && workspace.Points.SequenceEqual(original),"FailureKeepsPreviousPassport");
        try { operation.Apply(workspace,settings.Replace("\"manual\":28","\"manual\":29"),response,Array.Empty<QualityIssue>(),state); throw new Exception("Stale settings accepted"); } catch (InvalidOperationException) { }
        ModelTests.Assert(state.Current is null && workspace.Points.SequenceEqual(original),"Stale settings atomicity");
        operation.Apply(workspace,settings,response,Array.Empty<QualityIssue>(),state,()=>interpretation=settings);
        interpretation = "changed";
        var first = state.Current!;
        ModelTests.Assert(first.Matches(workspace.Points) && first.SourceName=="Исходная" && first.TargetName=="Целевая", "SuccessUsesCapturedSystems");
        var secondOperation = Operation();
        secondOperation.Apply(workspace,settings,response,Array.Empty<QualityIssue>(),state);
        ModelTests.Assert(state.Current != first,"New identity passport not captured");
        ModelTests.Assert(workspace.Undo() && ReferenceEquals(state.Current,first),"IdentityConversionHasUndo");
        ModelTests.Assert(workspace.Undo() && state.Current is null && interpretation==settings && workspace.Points.SequenceEqual(original),"TwoConversionsAndTwoUndos");
        operation = Operation(); operation.Apply(workspace,settings,response,Array.Empty<QualityIssue>(),state);
        first = state.Current!;
        workspace.SetPoint(0,workspace.Points[0] with { Description="edited" });
        ModelTests.Assert(!first.Matches(workspace.Points),"EditMarksPassportStale");
        workspace.Undo(); ModelTests.Assert(first.Matches(workspace.Points),"Undo edit restores correspondence");
        var errors = new[] { new QualityIssue(0,"error","bad") };
        try { Operation().Apply(workspace,settings,response,errors,state); throw new Exception("Error passport accepted"); } catch (ArgumentException) { }
        ModelTests.Assert(ReferenceEquals(first,state.Current) && first.Matches(workspace.Points),"Error keeps prior result");
        var noHistory = new PointWorkspace(); noHistory.ReplacePoints(original); noHistory.ReplacePoints(original);
        ModelTests.Assert(noHistory.Undo() && !noHistory.Undo(),"Ordinary same points must not add undo");
        Console.WriteLine("PASS passport workflow: successful/failed/stale/identity calculation, two undos and changed table");
    }
}
