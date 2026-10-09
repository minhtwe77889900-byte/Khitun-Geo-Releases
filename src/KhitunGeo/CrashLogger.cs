using System.Text;

namespace KhitunGeo;

internal static class CrashLogger
{
    private static readonly object Sync = new();

    public static string Write(Exception ex, string context = "Unhandled exception")
    {
        return WriteText($"{context}\r\n{ex}");
    }

    public static string WriteText(string text)
    {
        try
        {
            lock (Sync)
            {
                AppPaths.EnsureDirectories();
                var path = Path.Combine(AppPaths.LogsDirectory, $"KhitunGeo_{DateTime.Now:yyyyMMdd}.log");
                var sb = new StringBuilder();
                sb.AppendLine(new string('=', 72));
                sb.AppendLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                sb.AppendLine($"Windows: {Environment.OSVersion}");
                sb.AppendLine($".NET: {Environment.Version}");
                sb.AppendLine(text);
                File.AppendAllText(path, sb.ToString(), Encoding.UTF8);
                return path;
            }
        }
        catch
        {
            return string.Empty;
        }
    }

    public static string? GetLatestLog()
    {
        try
        {
            AppPaths.EnsureDirectories();
            return Directory.EnumerateFiles(AppPaths.LogsDirectory, "KhitunGeo_*.log")
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }
}
