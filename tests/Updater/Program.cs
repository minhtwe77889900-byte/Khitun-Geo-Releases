using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using KhitunGeo;

var payload = new byte[] { 77, 90, 1, 2, 3, 4, 5 }; // Inert test bytes, never executed.
var hash = Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
string Release(string tag = "v1.7.3", bool prerelease = false, string? digest = null, string? url = null) => JsonSerializer.Serialize(new
{
    tag_name = tag, draft = false, prerelease,
    assets = new[] { new { name = "KhitunGeo_Setup_1_7_3_x64.exe", size = payload.Length, state = "uploaded", digest = digest ?? "sha256:" + hash,
        browser_download_url = url ?? $"https://github.com/{UpdateService.Repository}/releases/download/{tag}/KhitunGeo_Setup_1_7_3_x64.exe" } }
});
void Check(bool value, string name) { if (!value) throw new Exception(name); Console.WriteLine("PASS " + name); }
void Reject(Action action, string name) { try { action(); } catch (InvalidDataException) { Console.WriteLine("PASS " + name); return; } throw new Exception(name); }
var current = new Version(1, 7, 2, 0);
var update = UpdateService.ParseRelease(Release(), current)!;
Check(update.Version == new Version(1, 7, 3), "new stable release accepted");
Check(UpdateService.ParseRelease(Release(), new Version(1, 7, 3, 0)) is null, "same version not reinstalled");
Check(UpdateService.ParseRelease(Release(), new Version(1, 8, 0)) is null, "downgrade rejected");
Check(UpdateService.ParseRelease(Release(prerelease: true), current) is null, "prerelease ignored");
Check(UpdateService.ParseRelease(Release(tag: "v1.7.3-preview"), current) is null, "preview tag ignored");
Reject(() => UpdateService.ParseRelease(Release(digest: "sha256:bad"), current), "missing valid digest prevents installation");
Reject(() => UpdateService.ParseRelease(Release(url: "https://example.com/installer.exe"), current), "foreign download URL rejected");
var directory = Path.Combine(Path.GetTempPath(), "khitun-updater-test-" + Guid.NewGuid().ToString("N"));
try
{
    using var handler = new FakeHandler(payload);
    using var client = new HttpClient(handler);
    var service = new UpdateService(client);
    var file = await service.DownloadAsync(update, directory, null, CancellationToken.None);
    Check(await UpdateService.VerifyAsync(file, update, CancellationToken.None), "download size and digest verified");
    await service.DownloadAsync(update, directory, null, CancellationToken.None);
    Check(handler.Calls == 1, "verified cached installer reused without traffic");
    await File.WriteAllBytesAsync(file, new byte[] { 77, 90, 9, 9, 9, 9, 9 });
    Check(!await UpdateService.VerifyAsync(file, update, CancellationToken.None), "same-size corruption detected");
    handler.Bytes = new byte[] { 77, 90, 0, 0, 0, 0, 0 };
    try { await service.DownloadAsync(update, directory, null, CancellationToken.None); throw new Exception("corrupt download accepted"); }
    catch (InvalidDataException) { Console.WriteLine("PASS corrupt download rejected"); }
    Check(Directory.GetFiles(directory, "*.part").Length == 0, "failed download leaves no partial file");
    using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
    try { await service.DownloadAsync(update, directory, null, cancelled.Token); throw new Exception("cancellation ignored"); }
    catch (OperationCanceledException) { Console.WriteLine("PASS cancellation propagated"); }
    handler.Bytes = payload.Concat(new byte[] { 8 }).ToArray();
    try { await service.DownloadAsync(update, directory, null, CancellationToken.None); throw new Exception("oversized download accepted"); }
    catch (InvalidDataException) { Console.WriteLine("PASS oversized download rejected"); }
}
finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }

sealed class FakeHandler(byte[] bytes) : HttpMessageHandler
{
    public byte[] Bytes { get; set; } = bytes;
    public int Calls { get; private set; }
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        token.ThrowIfCancellationRequested(); Calls++;
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { RequestMessage = request, Content = new ByteArrayContent(Bytes) });
    }
}
