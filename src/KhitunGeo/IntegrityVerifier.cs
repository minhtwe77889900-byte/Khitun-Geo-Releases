using System.Security.Cryptography;

namespace KhitunGeo;

internal sealed class IntegrityVerificationException : Exception
{
    public IntegrityVerificationException(string message) : base(message) { }
}

internal static class IntegrityVerifier
{
    private static readonly IReadOnlyDictionary<string, string> ExpectedHashes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["brand/khitun_geo.ico"] = "afb082d9014bb753e8df183990b91bbc454c85386b1d23513ca80dfaa012e94e",
            ["wwwroot/brand/khitun_geo.ico"] = "afb082d9014bb753e8df183990b91bbc454c85386b1d23513ca80dfaa012e94e",
            ["wwwroot/brand/khitun_geo_icon_128.png"] = "5ad84141d275fc4a0241ba20ed2ff31e818ed74dcb33e281d017ee885a02ccf3",
            ["wwwroot/brand/khitun_geo_icon_256.png"] = "a481527f3d40b55963128b6082da82c6a97ca5556db28cf34522eef37fb9b533",
            ["wwwroot/brand/khitun_geo_icon_32.png"] = "808e92a5e673d3bb672fa245c59b0da496313dae7a26204daf071f1fa5983b44",
            ["wwwroot/brand/khitun_geo_icon_512.png"] = "7c053de3e449aa6d37f01fe8ae22eb2383fe94643d671a91f55b0bfbf97e62ed",
            ["wwwroot/brand/khitun_geo_icon_64.png"] = "ee50cfe9abd4a0ed8bc03a33ae8067d7d9a53de7977cdf724694379cd34d4799",
            ["wwwroot/brand/khitun_geo_mark.svg"] = "8800a08028d9a8b12489247cf604cd591809c323837872e27015f79fabca0920",
            ["wwwroot/brand/khitun_geo_logo.png"] = "b19161b2d8643cecec41fde14a20066f2c0fb18b89c13f9176d7bf879be7b85b",
            ["wwwroot/example_points.geojson"] = "2f1562d69af27eff3c902145ffb58185ce34cdbcb59bf2177d595f9473947a65",
            ["wwwroot/example_points.json"] = "0de20981fa11df53dca1bb2f82b362cf541ee835e7e31cfce29ddc5198a58461",
            ["wwwroot/example_points_pnezd.csv"] = "11dfc57e1d19e101cc9340b530a2da765294af7fec0215434d648486dbb8b2cf",
            ["wwwroot/example_points_semicolon.csv"] = "225684b28b2af4ed77b9eda10147117e6d0fc5c3258347316b4a915fb1f61f61",
            ["wwwroot/coordinate-adapter.js"] = "58f8c9f23951c769ee8412e8fbcdf6353e6c5a33a9f75aeb8e899c84b8e0bbce",
            ["wwwroot/coordinate-core.js"] = "351c8c0c19e208e81d05e5a39762492a955d8d657416ffe673571c2b3c94943a",
            ["wwwroot/workspace-core.js"] = "a84904f0ad1cd5ec63531ac2be473d0be8061103154f3ab4cf0d06463e0115b8",
            ["wwwroot/crs/regional-updates.json"] = "1a8f4730ac7a7b4ac49e9765bfdd509b5229b2789b91b44505725a807b779d6b",
            ["wwwroot/crs/native-systems.json"] = "627cf926d845a020d225041c37d2e489f7e9b3b5b91873af2b4ba71c8e8f6836",
            ["wwwroot/crs/msk.json"] = "e6be34ce2a6d36d9a018164f3d69c76d74e5be89ecae017ded2fd835b6a4449f",
            ["wwwroot/crs/sk63.json"] = "f3345219ec7bc788a29ba954cd87e0b143091cb6dbb879b045a3b72ddcee0dc7",
            ["wwwroot/crs/LICENSE-msk.txt"] = "4f2416509c30c4f0c4de101cc30c571e3be33fdb5c42cabc0d2915e8ed5ea51e",
            ["wwwroot/crs/LICENSE-sk63.txt"] = "c71d239df91726fc519c6eb72d318ec65820627232b2f796219e87dcf35d0ab4",
            ["wwwroot/crs/NOTICE.txt"] = "2147eb98851e1082fceea314afacc0cbb9044eafd2164533cac85020346e39a4",
        };

    public static int ProtectedFileCount => ExpectedHashes.Count;

    public static string GetStatusText(string baseDirectory)
    {
        try
        {
            VerifyOrThrow(baseDirectory);
            return $"целостность подтверждена ({ProtectedFileCount} файлов)";
        }
        catch (Exception ex)
        {
            return "целостность нарушена: " + ex.Message.Split('\n')[0].Trim();
        }
    }

    public static void VerifyOrThrow(string baseDirectory)
    {
        var failures = new List<string>();

        foreach (var (relativePath, expectedHash) in ExpectedHashes)
        {
            var path = Path.Combine(baseDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path))
            {
                failures.Add($"не найден файл: {relativePath}");
                continue;
            }

            var actualHash = GetSha256(path);
            if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
                failures.Add($"изменён файл: {relativePath}");
        }

        if (failures.Count == 0)
            return;

        throw new IntegrityVerificationException(
            "Проверка целостности Khitun Geo не пройдена.\n\n" +
            string.Join("\n", failures) +
            "\n\nПереустановите приложение из официального установщика.");
    }

    private static string GetSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }
}
