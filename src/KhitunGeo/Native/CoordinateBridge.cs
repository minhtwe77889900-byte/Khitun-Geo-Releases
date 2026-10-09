using System.Text.Json;

namespace KhitunGeo.Native;

internal sealed record CoordinateZoneOptions(bool Auto, double? Manual, bool SourcePrefix, bool TargetPrefix);

// Transport and state validation only. Coordinate mathematics stays in JavaScript.
internal static class CoordinateBridge
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static string BuildRequest(IReadOnlyList<SurveyPoint> points, JsonElement source,
        JsonElement target, CoordinateZoneOptions zone)
    {
        if (source.ValueKind != JsonValueKind.Object || target.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("Не заданы параметры систем координат.");
        if (zone.Manual.HasValue && !double.IsFinite(zone.Manual.Value))
            throw new ArgumentException("Некорректная зона.");
        foreach (var point in points)
        {
            PointWorkspace.ValidatePoint(point);
            if (!point.X.HasValue || !point.Y.HasValue || point.Name is null || point.Description is null)
                throw new ArgumentException("Заполните X и Y всех точек.");
        }
        return JsonSerializer.Serialize(new
        {
            version = 1, source, target, zone,
            points = points.Select(p => new { name = p.Name, x = p.X!.Value, y = p.Y!.Value, height = p.Height, description = p.Description })
        }, JsonOptions);
    }

    public static IReadOnlyList<int> ApplyResponse(PointWorkspace workspace, IReadOnlyList<SurveyPoint> snapshot, string json, Action? onUndo = null, bool recordUnchanged = false)
    {
        if (!workspace.Points.SequenceEqual(snapshot))
            throw new InvalidOperationException("Таблица изменилась во время расчёта. Повторите преобразование.");
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.GetProperty("version").GetInt32() != 1) throw new FormatException("Неподдерживаемая версия ответа.");
        var rows = root.GetProperty("points");
        if (rows.GetArrayLength() != snapshot.Count) throw new FormatException("Расчёт вернул другое количество точек.");
        var zones = root.GetProperty("targetZones").EnumerateArray().Select(z => z.GetInt32()).ToArray();
        if (zones.Any(z => z < 1 || z > 120)) throw new FormatException("Некорректная зона в ответе.");
        var next = new SurveyPoint[snapshot.Count];
        for (var i = 0; i < next.Length; i++)
        {
            var row = rows[i];
            var heightElement = row.GetProperty("height");
            double? height = heightElement.ValueKind == JsonValueKind.Null ? null : heightElement.GetDouble();
            var name = row.GetProperty("name").GetString();
            var description = row.GetProperty("description").GetString();
            if (name != snapshot[i].Name || description != snapshot[i].Description || height != snapshot[i].Height)
                throw new FormatException("Расчёт изменил номер, описание или высоту точки.");
            next[i] = snapshot[i] with { X = row.GetProperty("x").GetDouble(), Y = row.GetProperty("y").GetDouble() };
            PointWorkspace.ValidatePoint(next[i]);
        }
        // One state replacement after every row has passed validation; one undo command.
        workspace.ReplacePoints(next, onUndo, recordUnchanged);
        return Array.AsReadOnly(zones);
    }
}
