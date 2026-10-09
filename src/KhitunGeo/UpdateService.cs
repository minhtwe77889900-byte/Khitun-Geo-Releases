using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace KhitunGeo;

internal sealed record AppUpdate(Version Version, string FileName, Uri DownloadUri, long Size, string Sha256);

internal sealed class UpdateService
{
    internal const string Repository = "minhtwe77889900-byte/Khitun-Geo-Releases";
    internal const string ReleasePage = "https://github.com/" + Repository + "/releases/latest";
    private const long MaxInstallerBytes = 512L * 1024 * 1024;
    private readonly HttpClient _http;

    public UpdateService(HttpClient http) => _http = http;

    public static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("KhitunGeo-Updater/1.7.11");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }

    // A complete, stable release from our repository only; no branches or prereleases.
    internal static AppUpdate? ParseRelease(string json, Version installed)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.GetProperty("draft").GetBoolean() || root.GetProperty("prerelease").GetBoolean()) return null;
        var tag = root.GetProperty("tag_name").GetString() ?? "";
        if (!Regex.IsMatch(tag, @"^v\d+\.\d+\.\d+$") || !Version.TryParse(tag[1..], out var version)) return null;
        var current = new Version(installed.Major, installed.Minor, Math.Max(0, installed.Build));
        if (version <= current) return null;
        var name = $"KhitunGeo_Setup_{version.Major}_{version.Minor}_{version.Build}_x64.exe";
        var expectedUrl = $"https://github.com/{Repository}/releases/download/{tag}/{name}";
        foreach (var asset in root.GetProperty("assets").EnumerateArray())
        {
            if (asset.GetProperty("name").GetString() != name) continue;
            var size = asset.GetProperty("size").GetInt64();
            var digest = asset.TryGetProperty("digest", out var d) ? d.GetString() ?? "" : "";
            if (asset.GetProperty("state").GetString() != "uploaded" || size <= 0 || size > MaxInstallerBytes ||
                asset.GetProperty("browser_download_url").GetString() != expectedUrl ||
                !Regex.IsMatch(digest, @"^sha256:[a-fA-F0-9]{64}$"))
                throw new InvalidDataException("Релиз ещё не готов к автоматическому обновлению. Попробуйте позже.");
            return new AppUpdate(version, name, new Uri(expectedUrl), size, digest[7..].ToLowerInvariant());
        }
        throw new InvalidDataException("Установщик новой версии ещё не опубликован. Попробуйте позже.");
    }

    public async Task<AppUpdate?> CheckAsync(Version installed, CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        using var response = await _http.GetAsync($"https://api.github.com/repos/{Repository}/releases/latest", timeout.Token);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return ParseRelease(await response.Content.ReadAsStringAsync(timeout.Token), installed);
    }

    public static async Task<bool> VerifyAsync(string path, AppUpdate update, CancellationToken token)
    {
        if (!File.Exists(path) || new FileInfo(path).Length != update.Size) return false;
        await using var stream = File.OpenRead(path);
        var hash = Convert.ToHexString(await SHA256.HashDataAsync(stream, token));
        return string.Equals(hash, update.Sha256, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<string> DownloadAsync(AppUpdate update, string directory, IProgress<int>? progress, CancellationToken token)
    {
        Directory.CreateDirectory(directory);
        var destination = Path.Combine(directory, update.FileName);
        if (await VerifyAsync(destination, update, token)) return destination;
        var partial = destination + "." + Guid.NewGuid().ToString("N") + ".part";
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromMinutes(30));
        try
        {
            using var response = await _http.GetAsync(update.DownloadUri, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            response.EnsureSuccessStatusCode();
            if (response.RequestMessage?.RequestUri?.Scheme != "https") throw new InvalidDataException("Незащищённая загрузка запрещена.");
            if (response.Content.Headers.ContentLength is long length && length != update.Size)
                throw new InvalidDataException("Размер установщика не совпадает с опубликованным.");
            await using (var source = await response.Content.ReadAsStreamAsync(timeout.Token))
            await using (var target = new FileStream(partial, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
            {
                var buffer = new byte[81920];
                long received = 0;
                var lastPercent = -1;
                int read;
                while ((read = await source.ReadAsync(buffer.AsMemory(), timeout.Token)) != 0)
                {
                    received += read;
                    if (received > update.Size) throw new InvalidDataException("Установщик превышает ожидаемый размер.");
                    await target.WriteAsync(buffer.AsMemory(0, read), timeout.Token);
                    var percent = (int)(received * 100 / update.Size);
                    if (percent != lastPercent) { progress?.Report(percent); lastPercent = percent; }
                }
            }
            if (!await VerifyAsync(partial, update, timeout.Token))
                throw new InvalidDataException("Проверка SHA-256 не пройдена. Обновление не будет запущено.");
            File.Move(partial, destination, true);
            return destination;
        }
        finally { if (File.Exists(partial)) File.Delete(partial); }
    }
}
