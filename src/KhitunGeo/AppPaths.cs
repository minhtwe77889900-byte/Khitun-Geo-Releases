namespace KhitunGeo;

internal static class AppPaths
{
    public static string AppDataDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Khitun Geo");

    public static string LogsDirectory => Path.Combine(AppDataDirectory, "Logs");
    public static string SettingsFile => Path.Combine(AppDataDirectory, "settings.json");
    public static string UpdatesDirectory => Path.Combine(AppDataDirectory, "Updates");
    public static string DefaultExportDirectory => Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(AppDataDirectory);
        Directory.CreateDirectory(LogsDirectory);
        Directory.CreateDirectory(UpdatesDirectory);
    }
}
