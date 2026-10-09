using System.Text.Json;

namespace KhitunGeo;

internal sealed class AppSettings
{
    public string ExportFolder { get; set; } = AppPaths.DefaultExportDirectory;
    public bool RememberLastExportFolder { get; set; } = true;
    public bool AutoUpdateEnabled { get; set; } = true;
}

internal static class AppSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static AppSettings Load()
    {
        try
        {
            AppPaths.EnsureDirectories();
            if (!File.Exists(AppPaths.SettingsFile))
                return new AppSettings();

            var json = File.ReadAllText(AppPaths.SettingsFile);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            if (string.IsNullOrWhiteSpace(settings.ExportFolder))
                settings.ExportFolder = AppPaths.DefaultExportDirectory;
            return settings;
        }
        catch
        {
            return new AppSettings();
        }
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            AppPaths.EnsureDirectories();
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(AppPaths.SettingsFile, json);
        }
        catch
        {
            // Настройки не должны мешать основной работе приложения.
        }
    }
}
