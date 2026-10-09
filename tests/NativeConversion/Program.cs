using System.Text.Json;
using KhitunGeo.Native;
static void Check(bool value, string message) { if (!value) throw new Exception(message); }
var original = new[] { new SurveyPoint("001", 65, 84, -12, "Глубина"), new SurveyPoint("002", 65.01, 84.02, null, "") };
using var geo = JsonDocument.Parse("{\"kind\":\"geo\",\"datum\":\"wgs\"}");
using var target = JsonDocument.Parse("{\"kind\":\"gk6\",\"datum\":\"sk42\"}");
var settings = CoordinateBridge.BuildRequest(Array.Empty<SurveyPoint>(), geo.RootElement, target.RootElement, new(true, 28, true, true));
const string response = "{\"version\":1,\"targetZones\":[15],\"points\":[{\"name\":\"001\",\"x\":7000000,\"y\":15450000,\"height\":-12,\"description\":\"Глубина\"},{\"name\":\"002\",\"x\":7000100,\"y\":15450100,\"height\":null,\"description\":\"\"}]}";
var workspace = new PointWorkspace(); workspace.ReplacePoints(original);
var operation = new CoordinateConversion(workspace.Points, settings);
using (var request = JsonDocument.Parse(operation.Request)) Check(request.RootElement.GetProperty("points").GetArrayLength() == 2, "Captured request");
foreach (var changed in new[] { settings.Replace("\"auto\":true", "\"auto\":false"), settings.Replace("\"manual\":28", "\"manual\":29"), settings.Replace("\"targetPrefix\":true", "\"targetPrefix\":false"), settings.Replace("\"datum\":\"sk42\"", "\"datum\":\"sk95\"") })
{
    try { operation.Apply(workspace, changed, response); throw new Exception("Stale settings accepted"); } catch (InvalidOperationException) { }
    Check(workspace.Points.SequenceEqual(original), "Settings rejection mutated table");
}
workspace.SetPoint(0, original[0] with { X = 66 });
try { operation.Apply(workspace, settings, response); throw new Exception("Stale point accepted"); } catch (InvalidOperationException) { }
Check(workspace.Points[0].X == 66, "Point rejection overwrote edit");
workspace.Undo();
var interpretation = settings;
var zones = operation.Apply(workspace, settings, response, () => interpretation = settings);
interpretation = settings.Replace("\"datum\":\"wgs\"", "\"datum\":\"sk42\"");
Check(zones.SequenceEqual(new[] { 15 }) && workspace.Points[0].Height == -12 && workspace.Points[1].Height is null, "Conversion / zones / heights");
Check(workspace.Undo() && workspace.Points.SequenceEqual(original) && interpretation == settings, "Undo restores points and source interpretation");
var repeated = new CoordinateConversion(workspace.Points, interpretation);
using (var request = JsonDocument.Parse(repeated.Request)) Check(request.RootElement.GetProperty("source").GetProperty("datum").GetString() == "wgs", "Convert after undo uses original source");
repeated.Apply(workspace, interpretation, response);
Check(workspace.Undo() && workspace.Points.SequenceEqual(original), "Repeat conversion undo");
try { operation.Apply(workspace, settings, response.Replace("7000100", "null")); throw new Exception("Partial reply accepted"); } catch (Exception ex) when (ex is InvalidOperationException or FormatException) { }
Check(workspace.Points.SequenceEqual(original), "Partial failure mutated table");
Console.WriteLine("PASS native conversion: captured request, changed CRS/zone/prefix and point rejection, atomic apply, zones, heights and undo");
