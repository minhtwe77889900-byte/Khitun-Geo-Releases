using Jint;
using System.IO;
using System.Threading;
using System.Text.Json;
using KhitunGeo.Native;

static void Check(bool value, string name) { if (!value) throw new Exception(name); }
static void Near(JsonElement actual, JsonElement expected, string name)
{
    if (expected.ValueKind == JsonValueKind.Array)
    {
        Check(actual.ValueKind == JsonValueKind.Array && actual.GetArrayLength() == expected.GetArrayLength(), name);
        for (var i = 0; i < expected.GetArrayLength(); i++) Near(actual[i], expected[i], name);
    }
    else if (expected.ValueKind == JsonValueKind.Number)
    {
        var a = actual.GetDouble(); var e = expected.GetDouble();
        Check(double.IsFinite(a) && Math.Abs(a - e) <= Math.Max(1e-8, Math.Abs(e) * 2e-12), $"{name}: actual={a:R}, expected={e:R}, difference={Math.Abs(a-e):R}");
    }
    else Check(actual.GetRawText() == expected.GetRawText(), name);
}
var root = AppContext.BaseDirectory;
var runtime = new EmbeddedCoordinateRuntime(File.ReadAllText(Path.Combine(root, "coordinate-core.js")), File.ReadAllText(Path.Combine(root, "coordinate-adapter.js")));
var engine = runtime.CreateEngine();
Check(engine.Evaluate("typeof document + ':' + typeof window + ':' + typeof fetch + ':' + typeof System").AsString() == "undefined:undefined:undefined:undefined", "Unexpected host services");
engine.Execute("function khitunControl(json) { const c=JSON.parse(json); return JSON.stringify(KhitunCoordinates[c.operation](...c.args.map(v=>v&&typeof v==='object'&&v.ellipsoid?KhitunCoordinates.E[v.ellipsoid]:v))); }");
using var controls = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "controls.json")));
foreach (var control in controls.RootElement.EnumerateArray())
{
    using var actual = JsonDocument.Parse(engine.Invoke("khitunControl", control.GetRawText()).AsString());
    Near(actual.RootElement, control.GetProperty("expected"), control.GetProperty("name").GetString()!);
}
using var systems = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "native-systems.json")));
using var geo = JsonDocument.Parse("{\"kind\":\"geo\",\"datum\":\"wgs\"}");
var zone = new CoordinateZoneOptions(true, 28, true, true);
foreach (var system in systems.RootElement.GetProperty("systems").EnumerateArray())
{
    var request = CoordinateBridge.BuildRequest(Array.Empty<SurveyPoint>(), geo.RootElement, system.GetProperty("definition"), zone);
    using var response = JsonDocument.Parse(engine.Invoke("khitunConvertJson", request).AsString());
    Check(response.RootElement.GetProperty("points").GetArrayLength() == 0, "Catalogue validation");
}
using var proj = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "crs-proj-controls.json")));
double maximumError = 0;
foreach (var control in proj.RootElement.EnumerateArray())
{
    var family = control.GetProperty("family").GetString()!;
    var name = control.GetProperty("record").GetString();
    var system = systems.RootElement.GetProperty("systems").EnumerateArray().Single(s => s.GetProperty("id").GetString()!.StartsWith(family + ":", StringComparison.Ordinal) && s.GetProperty("name").GetString() == name);
    var input = control.GetProperty("input"); var expected = control.GetProperty("expected");
    var points = new[] { new SurveyPoint("001", input[0].GetDouble(), input[1].GetDouble(), input[2].GetDouble(), "Контроль") };
    var request = CoordinateBridge.BuildRequest(points, geo.RootElement, system.GetProperty("definition"), zone);
    using var response = JsonDocument.Parse(engine.Invoke("khitunConvertJson", request).AsString());
    var row = response.RootElement.GetProperty("points")[0];
    var dx = row.GetProperty("x").GetDouble() - expected[0].GetDouble();
    var dy = row.GetProperty("y").GetDouble() - expected[1].GetDouble();
    var error = Math.Sqrt(dx * dx + dy * dy); maximumError = Math.Max(maximumError, error);
    Check(double.IsFinite(error) && error < 0.002, name + ": " + error + " m");
    Check(row.GetProperty("height").GetDouble() == points[0].Height, "PROJ height changed");
}
var target = systems.RootElement.GetProperty("systems").EnumerateArray().First(s => s.GetProperty("definition").GetProperty("kind").GetString() == "fixedtm").GetProperty("definition");
var snapshot = new[] { new SurveyPoint("001", 65, 84, -12, "Глубина ' ; throw 1; //"), new SurveyPoint("002", 65.01, 84.02, null, "") };
var workspace = new PointWorkspace(); workspace.ReplacePoints(snapshot);
var settings = CoordinateBridge.BuildRequest(Array.Empty<SurveyPoint>(), geo.RootElement, target, zone);
var conversion = new CoordinateConversion(snapshot, settings);
var result = runtime.Convert(conversion.Request);
conversion.Apply(workspace, settings, result);
Check(workspace.Points[0].X != snapshot[0].X && workspace.Points[0].Description == snapshot[0].Description && workspace.Points[0].Height == -12 && workspace.Points[1].Height is null, "Embedded bridge / data isolation");
Check(workspace.Undo() && workspace.Points.SequenceEqual(snapshot), "Embedded conversion undo");
using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
try { runtime.Convert("{}", cancelled.Token); throw new Exception("Cancellation accepted"); } catch (OperationCanceledException) { }
try { runtime.Convert("{"); throw new Exception("Malformed JSON accepted"); } catch (Jint.Runtime.JavaScriptException) { }
Check(workspace.Points.SequenceEqual(snapshot), "Runtime failure mutated workspace");
using (var duringExecution = new CancellationTokenSource())
{
    var cancellable = runtime.CreateEngine(duringExecution.Token);
    duringExecution.CancelAfter(TimeSpan.FromMilliseconds(50));
    try { cancellable.Execute("while (true) {}"); throw new Exception("Running script ignored cancellation"); }
    catch (Jint.Runtime.ExecutionCanceledException) when (duringExecution.IsCancellationRequested) { }
}
using (var duringHostLoad = new CancellationTokenSource(TimeSpan.FromMilliseconds(50)))
{
    var slowHost = new EmbeddedCoordinateRuntime("while (true) {}", "");
    try { slowHost.Convert("{}", duringHostLoad.Token); throw new Exception("Host ignored cancellation"); }
    catch (OperationCanceledException ex) { Check(ex.CancellationToken == duringHostLoad.Token, "Lost cancellation token"); }
}
static void ConstraintStops(Engine limited, string script, string expectedType)
{
    try { limited.Execute(script); throw new Exception("Missing constraint: " + expectedType); }
    catch (Exception ex) when (ex.GetType().Name == expectedType) { }
}
ConstraintStops(new Engine(o => o.MaxStatements(100).TimeoutInterval(TimeSpan.FromSeconds(1))), "while (true) {}", "StatementsCountOverflowException");
ConstraintStops(new Engine(o => o.TimeoutInterval(TimeSpan.FromMilliseconds(50))), "while (true) {}", "TimeoutException");
ConstraintStops(new Engine(o => o.LimitMemory(1_000_000).TimeoutInterval(TimeSpan.FromSeconds(2))), "const values=[]; for(let i=0;i<100000;i++)values.push('x'.repeat(1024));", "MemoryLimitExceededException");
Console.WriteLine($"PASS Jint: {controls.RootElement.GetArrayLength()} frozen controls, {proj.RootElement.GetArrayLength()} PROJ controls (max {maximumError:R} m), catalogue, JSON bridge, cancellation, execution constraints and undo");
