using System.Text.Json;

namespace KhitunGeo.Native;

// UI-thread capture and apply; the worker receives only the immutable JSON request.
internal sealed class CoordinateConversion
{
    private readonly SurveyPoint[] points;
    private readonly string settings;
    public string Request { get; }

    public CoordinateConversion(IReadOnlyList<SurveyPoint> points, string settings)
    {
        this.points = points.ToArray();
        this.settings = settings;
        using var document = JsonDocument.Parse(settings);
        var root = document.RootElement;
        if (root.GetProperty("version").GetInt32() != 1 || root.GetProperty("points").GetArrayLength() != 0)
            throw new FormatException("Некорректный снимок настроек расчёта.");
        var zone = JsonSerializer.Deserialize<CoordinateZoneOptions>(root.GetProperty("zone").GetRawText(), new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new FormatException("Не заданы настройки зоны.");
        Request = CoordinateBridge.BuildRequest(this.points, root.GetProperty("source"), root.GetProperty("target"), zone);
    }

    public IReadOnlyList<int> Apply(PointWorkspace workspace, string currentSettings, string response, Action? onUndo = null, bool recordUnchanged = false)
    {
        if (!string.Equals(settings, currentSettings, StringComparison.Ordinal))
            throw new InvalidOperationException("Система координат или настройки зоны изменились во время расчёта. Повторите преобразование.");
        return CoordinateBridge.ApplyResponse(workspace, points, response, onUndo, recordUnchanged);
    }
}
