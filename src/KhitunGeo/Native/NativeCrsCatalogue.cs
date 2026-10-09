using System.IO;
using System.Text.Json;

namespace KhitunGeo.Native;

internal sealed record NativeCrsOption(string Id, string Name, JsonElement Definition);

internal static class NativeCrsCatalogue
{
    public static IReadOnlyList<NativeCrsOption> Read(string path)
    {
        using var stream = File.OpenRead(path);
        if (stream.Length > 4 * 1024 * 1024) throw new FormatException("Каталог СК слишком большой.");
        using var document = JsonDocument.Parse(stream);
        var root = document.RootElement;
        if (root.GetProperty("version").GetInt32() != 1) throw new FormatException("Неизвестная версия каталога СК.");
        var result = new List<NativeCrsOption>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in root.GetProperty("systems").EnumerateArray())
        {
            var id = row.GetProperty("id").GetString();
            var name = row.GetProperty("name").GetString();
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name) || !ids.Add(id))
                throw new FormatException("Повторяющаяся или пустая система координат.");
            var definition = row.GetProperty("definition");
            ValidateDefinition(definition);
            result.Add(new(id, name, definition.Clone()));
        }
        if (result.Count == 0) throw new FormatException("Пустой каталог СК.");
        return result.AsReadOnly();
    }

    private static double Number(JsonElement definition, string name)
    {
        var value = definition.GetProperty(name).GetDouble();
        if (!double.IsFinite(value)) throw new FormatException("Некорректный параметр СК: " + name);
        return value;
    }

    private static void ValidateDefinition(JsonElement definition)
    {
        var kind = definition.GetProperty("kind").GetString();
        var datum = definition.GetProperty("datum").GetString();
        if (kind is not ("geo" or "webmerc" or "gk6" or "gk3" or "utm" or "fixedtm"))
            throw new FormatException("Неразрешённая система координат.");
        if (datum is not ("wgs" or "gsk2011" or "sk42" or "sk95" or "pz90" or "pz9002" or "pz9011"))
            throw new FormatException("Неизвестный датум.");
        if ((kind is "utm" or "webmerc") && datum != "wgs") throw new FormatException("Требуется WGS-84.");
        if (kind != "fixedtm") return;
        if (Math.Abs(Number(definition, "lon0")) > 360 || Number(definition, "k") <= 0)
            throw new FormatException("Некорректная проекция.");
        Number(definition, "fe"); Number(definition, "fn");
        if (definition.TryGetProperty("lat0", out _) && Math.Abs(Number(definition, "lat0")) >= 90)
            throw new FormatException("Некорректная широта начала проекции.");
        if (definition.TryGetProperty("towgs", out var operation))
        {
            if (operation.GetArrayLength() != 7 || operation.EnumerateArray().Any(v => !double.IsFinite(v.GetDouble())))
                throw new FormatException("Некорректные параметры перехода.");
        }
        if (definition.TryGetProperty("ellipsoid", out var ellipsoid))
        {
            var name = ellipsoid.GetString();
            if (name is not ("krass" or "bessel") || (name == "krass" && datum is not ("sk42" or "sk95"))
                || (name == "bessel" && !definition.TryGetProperty("towgs", out _)))
                throw new FormatException("Несовместимый эллипсоид.");
        }
    }
}
