using System.Text.RegularExpressions;

namespace KhitunGeo.Native;

internal sealed record MskCrsEntry(string Id, string Name, IReadOnlyList<NativeCrsOption> Zones, bool IsMskGroup)
{
    public bool IsZoneGroup => IsMskGroup || Id == "sk63-group";
}

internal static class MskSelectionCatalogue
{
    public static IReadOnlyList<MskCrsEntry> Build(IReadOnlyList<NativeCrsOption> systems)
    {
        string GroupId(NativeCrsOption option)
        {
            if (option.Id.StartsWith("msk:", StringComparison.Ordinal))
                return "msk-group:" + option.Id[4..].Split('|')[0];
            if (option.Id.StartsWith("sk63:", StringComparison.Ordinal))
                return "sk63-group";
            return option.Id;
        }

        var options = systems.Where(s => s.Id != "msk164").Select(s =>
            s with { Name = Regex.Replace(s.Name, @"\s*·\s*[^·]*EPSG:.*$", "", RegexOptions.IgnoreCase) });
        return options.GroupBy(GroupId).Select(group =>
        {
            var zones = group.ToArray();
            var name = zones[0].Name;
            var isMsk = group.Key.StartsWith("msk-group:", StringComparison.Ordinal);
            if (isMsk) name = Regex.Replace(name, @"\s+зона\s+\S+(?:\s+\([^)]*\))?", "", RegexOptions.IgnoreCase);
            if (group.Key == "sk63-group") name = "СК-63";
            return new MskCrsEntry(group.Key, name, Array.AsReadOnly(zones), isMsk);
        }).ToArray();
    }
}
