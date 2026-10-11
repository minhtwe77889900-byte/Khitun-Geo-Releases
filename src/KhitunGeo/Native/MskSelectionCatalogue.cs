using System.Text.RegularExpressions;

namespace KhitunGeo.Native;

internal sealed record MskCrsEntry(string Id, string Name, IReadOnlyList<NativeCrsOption> Zones, bool IsMskGroup);

internal static class MskSelectionCatalogue
{
    public static IReadOnlyList<MskCrsEntry> Build(IReadOnlyList<NativeCrsOption> systems)
    {
        var result = new List<MskCrsEntry>();
        var added = new HashSet<string>();
        foreach (var option in systems)
        {
            if (!option.Id.StartsWith("msk:", StringComparison.Ordinal))
            {
                result.Add(new(option.Id, option.Name, new[] { option }, false));
                continue;
            }
            var region = option.Id.Split('|')[0];
            if (!added.Add(region)) continue;
            var zones = systems.Where(s => s.Id.Split('|')[0] == region).ToArray();
            var name = Regex.Replace(option.Name, @"\s+зона\s+\S+(?:\s+\([^)]*\))?", "", RegexOptions.IgnoreCase);
            result.Add(new("msk-group:" + region[4..], name, Array.AsReadOnly(zones), true));
        }
        return result.AsReadOnly();
    }
}
