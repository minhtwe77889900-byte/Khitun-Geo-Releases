using System.IO;
using System.Text.Json;
using KhitunGeo.Native;

static void Check(bool value, string name) { if (!value) throw new Exception(name); }
static void Rejected(Action action, string name) { try { action(); } catch (Exception ex) when (ex is FormatException or JsonException or InvalidOperationException or ArgumentException) { return; } throw new Exception(name); }
var points = new[] { new SurveyPoint("001", 65, 84, -12, "Глубина"), new SurveyPoint("002", 65.01, 84.02, null, "") };
using var source = JsonDocument.Parse("{\"kind\":\"geo\",\"datum\":\"wgs\"}");
using var target = JsonDocument.Parse("{\"kind\":\"fixedtm\",\"datum\":\"sk42\",\"lon0\":84,\"fe\":86209.8,\"fn\":-6542783.5,\"k\":1}");
var request = CoordinateBridge.BuildRequest(points, source.RootElement, target.RootElement, new(true, 28, true, true));
if (args.Length > 0 && args[0] == "--request") { Console.WriteLine(request); return; }
var workspace = new PointWorkspace(); workspace.ReplacePoints(points);
if (args.Length == 2 && args[0] == "--response")
{
    CoordinateBridge.ApplyResponse(workspace, points, File.ReadAllText(args[1]));
    Check(workspace.Points[0].X != points[0].X && workspace.Points[0].Height == -12 && workspace.Points[1].Height == null, "JS to C# response");
    Check(workspace.Undo() && workspace.Points.SequenceEqual(points), "Cross-host single undo");
    Console.WriteLine("PASS C# request → JavaScript conversion → C# atomic apply and undo"); return;
}
using (var document = JsonDocument.Parse(request))
{
    Check(document.RootElement.GetProperty("points")[0].GetProperty("x").GetDouble() == 65, "Northing order");
    Check(document.RootElement.GetProperty("points")[1].GetProperty("height").ValueKind == JsonValueKind.Null, "Missing height");
}
const string response = "{\"version\":1,\"targetZones\":[],\"points\":[{\"name\":\"001\",\"x\":7000000,\"y\":450000,\"height\":-12,\"description\":\"Глубина\"},{\"name\":\"002\",\"x\":7000100,\"y\":450100,\"height\":null,\"description\":\"\"}]}";
foreach (var invalid in new[] { "{", response.Replace("\"version\":1", "\"version\":2"), response.Replace("\"height\":-12", "\"height\":12"), response.Replace("\"name\":\"002\"", "\"name\":\"001\""), response.Replace("7000000", "null"), response.Replace("\"targetZones\":[]", "\"targetZones\":[0]") })
{
    Rejected(() => CoordinateBridge.ApplyResponse(workspace, points, invalid), "Invalid response accepted");
    Check(workspace.Points.SequenceEqual(points), "Invalid response mutated table");
}
CoordinateBridge.ApplyResponse(workspace, points, response);
Check(workspace.Points[0].X == 7000000 && workspace.Points[1].Height == null, "Converted rows");
Check(workspace.Undo() && workspace.Points.SequenceEqual(points), "One conversion, one undo");
workspace.SetPoint(0, points[0] with { X = 66 });
Rejected(() => CoordinateBridge.ApplyResponse(workspace, points, response), "Stale response accepted");
Check(workspace.Points[0].X == 66, "Stale response overwrote edit");
Console.WriteLine("PASS coordinate bridge: request schema, malformed/stale result rejection, metadata/Z preservation and atomic undo");
