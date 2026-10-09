using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace KhitunGeo.Native;

internal sealed record JsonPointTable(string[][] Rows, string? SourceCrsId);

internal static partial class JsonPointFileReader
{
    private static readonly Regex LabeledCoordinates = new(
        @"Широта\s*:\s*([+\-−]?\d+(?:[.,]\d+)?)\s+Долгота\s*:\s*([+\-−]?\d+(?:[.,]\d+)?)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static IReadOnlyList<SurveyPoint> Parse(string json)
    {
        JsonDocument document;
        try { document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 64 }); }
        catch (JsonException ex) { throw new FormatException("JSON-файл повреждён: " + ex.Message, ex); }
        using (document)
        {
            var root = document.RootElement;
            if (TryProperty(root, "pointInfoArray", out var pointInfo))
            {
                var legacy = ParsePointInfoArray(pointInfo);
                if (legacy.Count > 0) return legacy;
            }
            if (root.ValueKind == JsonValueKind.Object && TryProperty(root, "type", out var type) &&
                String(type).Equals("FeatureCollection", StringComparison.OrdinalIgnoreCase))
                return ParseGeoJson(root);

            var raw = root.ValueKind == JsonValueKind.Array ? root
                : root.ValueKind == JsonValueKind.Object && TryProperty(root, "points", out var points) && points.ValueKind == JsonValueKind.Array ? points
                : root.ValueKind == JsonValueKind.Object && TryProperty(root, "data", out var data) && data.ValueKind == JsonValueKind.Array ? data
                : default;
            if (raw.ValueKind != JsonValueKind.Array)
                throw new FormatException("JSON должен содержать массив points/data, GeoJSON FeatureCollection или pointInfoArray.");
            var result = new List<SurveyPoint>();
            foreach (var (point, index) in raw.EnumerateArray().Select((point, index) => (point, index)))
                result.Add(ParsePoint(point, index));
            if (result.Count == 0) throw new FormatException("В JSON не найдено ни одной точки.");
            return result;
        }
    }

    public static JsonPointTable ReadTable(string json)
    {
        JsonDocument document;
        try { document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 64 }); }
        catch (JsonException ex) { throw new FormatException("JSON-файл повреждён: " + ex.Message, ex); }
        using (document)
        {
            var root = document.RootElement;
            var source = GetSourceCrs(root);
            if (TryProperty(root, "pointInfoArray", out var pointInfo))
                return ReadLegacyTable(pointInfo, source ?? "wgs");
            if (TryProperty(root, "type", out var type) && String(type).Equals("FeatureCollection", StringComparison.OrdinalIgnoreCase))
                return FromPoints(ParseGeoJson(root), source);

            var raw = root.ValueKind == JsonValueKind.Array ? root
                : TryProperty(root, "points", out var points) && points.ValueKind == JsonValueKind.Array ? points
                : TryProperty(root, "data", out var data) && data.ValueKind == JsonValueKind.Array ? data
                : default;
            if (raw.ValueKind != JsonValueKind.Array)
                throw new FormatException("JSON должен содержать массив points/data, GeoJSON FeatureCollection или pointInfoArray.");
            var items = raw.EnumerateArray().ToArray();
            if (items.Length == 0) throw new FormatException("В JSON не найдено ни одной точки.");
            if (items.All(item => item.ValueKind == JsonValueKind.Object))
            {
                var keys = items.SelectMany(item => item.EnumerateObject().Select(property => property.Name))
                    .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                var rows = new List<string[]> { keys };
                rows.AddRange(items.Select(item => keys.Select(key => TryProperty(item, key, out var value) ? String(value) : "").ToArray()));
                return new(rows.ToArray(), source);
            }
            if (items.All(item => item.ValueKind == JsonValueKind.Array))
                return new(items.Select(item => item.EnumerateArray().Select(String).ToArray()).ToArray(), source);
            return FromPoints(Parse(json), source);
        }
    }

    private static JsonPointTable FromPoints(IReadOnlyList<SurveyPoint> points, string? source)
    {
        var rows = new List<string[]> { new[] { "№ точки", "X", "Y", "Z", "Описание" } };
        rows.AddRange(points.Select(point => new[] { point.Name, point.X?.ToString("R", CultureInfo.InvariantCulture) ?? "",
            point.Y?.ToString("R", CultureInfo.InvariantCulture) ?? "", point.Height?.ToString("R", CultureInfo.InvariantCulture) ?? "", point.Description }));
        return new(rows.ToArray(), source);
    }

    private static JsonPointTable ReadLegacyTable(JsonElement value, string source)
    {
        if (value.ValueKind != JsonValueKind.Array) throw new FormatException("Поле pointInfoArray должно быть массивом.");
        var columns = value.EnumerateArray().Select(column => column.ValueKind == JsonValueKind.Array
            ? column.EnumerateArray().Select(String).ToArray() : Array.Empty<string>()).ToArray();
        var count = columns.Select(column => column.Length).DefaultIfEmpty().Max();
        if (count == 0) throw new FormatException("В pointInfoArray нет записей.");
        var rows = new List<string[]> { new[] { "№ точки", "X", "Y", "Z", "Описание" } };
        for (var i = 0; i < count; i++)
        {
            var marker = Regex.Match(At(columns, 0, i), @"#\s*:\s*(.+)$", RegexOptions.CultureInvariant);
            var name = marker.Success ? marker.Groups[1].Value.Trim() : (i + 1).ToString(CultureInfo.InvariantCulture);
            var labeled = LabeledCoordinates.Match(At(columns, 1, i));
            var precise = At(columns, 2, i).Split(',').Select(part => part.Trim()).ToArray();
            var x = labeled.Success ? labeled.Groups[1].Value : precise.Length == 2 ? precise[0] : "";
            var y = labeled.Success ? labeled.Groups[2].Value : precise.Length == 2 ? precise[1] : "";
            var description = new[] { At(columns, 4, i), At(columns, 5, i), At(columns, 6, i), At(columns, 8, i) }
                .Select(part => part.Trim()).Where(part => part.Length > 0);
            rows.Add(new[] { name, x, y, At(columns, 3, i).Trim(), string.Join(" · ", description) });
        }
        return new(rows.ToArray(), source);
    }

    private static string? GetSourceCrs(JsonElement root)
    {
        if (TryProperty(root, "source", out var source) && source.ValueKind == JsonValueKind.String) return String(source);
        if (TryProperty(root, "crs", out var crs))
        {
            if (crs.ValueKind == JsonValueKind.String) return String(crs);
            if (TryProperty(crs, "id", out var id)) return String(id);
            if (TryProperty(crs, "name", out var name)) return String(name);
            if (TryProperty(crs, "properties", out var properties) && TryProperty(properties, "name", out var nestedName)) return String(nestedName);
        }
        return null;
    }

    private static SurveyPoint ParsePoint(JsonElement point, int index)
    {
        if (point.ValueKind == JsonValueKind.Array)
        {
            var fields = point.EnumerateArray().ToArray();
            return new SurveyPoint(Value(fields, 0, (index + 1).ToString(CultureInfo.InvariantCulture)),
                Number(fields, 1), Number(fields, 2), Number(fields, 3), Value(fields, 4, ""));
        }
        if (point.ValueKind != JsonValueKind.Object) throw new FormatException($"Запись {index + 1}: ожидался объект или массив точки.");
        return new SurveyPoint(
            GetString(point, index + 1, "no", "number", "id", "point", "name", "№"),
            GetNumber(point, "x", "X", "b", "B", "lat", "latitude", "northing", "N"),
            GetNumber(point, "y", "Y", "l", "L", "lon", "lng", "longitude", "easting", "E"),
            GetNumber(point, "z", "Z", "h", "H", "height", "elevation", "alt", "altitude"),
            GetString(point, "", "d", "D", "description", "desc", "code", "note", "comment"));
    }

    private static IReadOnlyList<SurveyPoint> ParseGeoJson(JsonElement root)
    {
        if (!TryProperty(root, "features", out var features) || features.ValueKind != JsonValueKind.Array)
            throw new FormatException("GeoJSON FeatureCollection не содержит массива features.");
        var result = new List<SurveyPoint>();
        foreach (var feature in features.EnumerateArray())
        {
            if (!TryProperty(feature, "geometry", out var geometry) ||
                !TryProperty(geometry, "type", out var geometryType) ||
                !String(geometryType).Equals("Point", StringComparison.OrdinalIgnoreCase) ||
                !TryProperty(geometry, "coordinates", out var coordinates) || coordinates.ValueKind != JsonValueKind.Array)
                continue;
            var coordinate = coordinates.EnumerateArray().ToArray();
            if (coordinate.Length < 2) continue;
            var properties = TryProperty(feature, "properties", out var props) && props.ValueKind == JsonValueKind.Object ? props : default;
            var index = result.Count + 1;
            result.Add(new SurveyPoint(GetString(properties, index, "no", "number", "id", "name"),
                Numeric(coordinate[1]), Numeric(coordinate[0]), coordinate.Length > 2 ? Numeric(coordinate[2]) : GetNumber(properties, "z", "elevation"),
                GetString(properties, "", "description", "desc", "code")));
        }
        if (result.Count == 0) throw new FormatException("В GeoJSON не найдено точек типа Point.");
        return result;
    }

    // Legacy surveying JSON stores each point field as a separate parallel array.
    // Keep its coordinate fallback and description fields compatible with the old importer.
    private static IReadOnlyList<SurveyPoint> ParsePointInfoArray(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Array) return Array.Empty<SurveyPoint>();
        var columns = value.EnumerateArray().Select(column => column.ValueKind == JsonValueKind.Array
            ? column.EnumerateArray().Select(String).ToArray() : Array.Empty<string>()).ToArray();
        var count = columns.Select(column => column.Length).DefaultIfEmpty().Max();
        var result = new List<SurveyPoint>();
        for (var i = 0; i < count; i++)
        {
            var nameText = At(columns, 0, i);
            var marker = Regex.Match(nameText, @"#\s*:\s*(.+)$", RegexOptions.CultureInvariant);
            var name = marker.Success ? marker.Groups[1].Value.Trim() : (i + 1).ToString(CultureInfo.InvariantCulture);
            var precise = At(columns, 2, i).Split(',').Select(part => part.Trim()).ToArray();
            var latText = precise.ElementAtOrDefault(0) ?? "";
            var lonText = precise.ElementAtOrDefault(1) ?? "";
            if (!IsNumeric(latText) || !IsNumeric(lonText))
            {
                var match = LabeledCoordinates.Match(At(columns, 1, i));
                if (!match.Success) continue;
                latText = match.Groups[1].Value; lonText = match.Groups[2].Value;
            }
            var heightText = At(columns, 3, i).Trim();
            var height = IsNumeric(heightText) ? ParseNumber(heightText) : 0;
            var description = new[] { At(columns, 4, i), At(columns, 5, i), At(columns, 6, i), At(columns, 8, i) }
                .Select(part => part.Trim()).Where(part => part.Length > 0);
            result.Add(new SurveyPoint(name, ParseNumber(latText), ParseNumber(lonText), height, string.Join(" · ", description)));
        }
        return result;
    }

    private static bool TryProperty(JsonElement element, string name, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
            foreach (var property in element.EnumerateObject())
                if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) { value = property.Value; return true; }
        value = default; return false;
    }

    private static string GetString(JsonElement element, int fallback, params string[] keys) => GetString(element, fallback.ToString(CultureInfo.InvariantCulture), keys);
    private static string GetString(JsonElement element, string fallback, params string[] keys)
    {
        foreach (var key in keys) if (TryProperty(element, key, out var value) && value.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined)) return String(value);
        return fallback;
    }

    private static double? GetNumber(JsonElement element, params string[] keys)
    {
        foreach (var key in keys)
            if (TryProperty(element, key, out var value) && value.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined)) return Numeric(value);
        return null;
    }

    private static double? Number(JsonElement[] values, int index) => index < values.Length ? Numeric(values[index]) : null;
    private static string Value(JsonElement[] values, int index, string fallback) => index < values.Length && values[index].ValueKind != JsonValueKind.Null ? String(values[index]) : fallback;
    private static double? Numeric(JsonElement value) => value.ValueKind == JsonValueKind.Number
        ? value.GetDouble() : value.ValueKind == JsonValueKind.String ? TabularPaste.Number(value.GetString() ?? "") : null;
    private static string String(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString() ?? "",
        JsonValueKind.Number => value.GetRawText(),
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        _ => ""
    };
    private static bool IsNumeric(string value) => double.TryParse(value.Trim().Replace('−', '-').Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && double.IsFinite(number);
    private static double ParseNumber(string value) => double.Parse(value.Trim().Replace('−', '-').Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture);
    private static string At(string[][] columns, int column, int row) => column < columns.Length && row < columns[column].Length ? columns[column][row] : "";
}
