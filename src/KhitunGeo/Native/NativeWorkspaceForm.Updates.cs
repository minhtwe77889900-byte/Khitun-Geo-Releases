using System.Diagnostics;
using System.Reflection;
using Microsoft.Win32;

namespace KhitunGeo.Native;

internal sealed partial class NativeWorkspaceForm
{
    private readonly CancellationTokenSource updateLifetime = new();
    private readonly Button cancelUpdateButton = new()
    {
        Text = "Отменить загрузку обновления", Width = 130, Height = 40,
        FlatStyle = FlatStyle.Flat, Visible = false
    };
    private readonly Button installUpdateButton = new()
    {
        Text = "Установить обновление", Width = 130, Height = 40,
        FlatStyle = FlatStyle.Flat, Visible = false, Enabled = false
    };
    private CancellationTokenSource? updateOperation;
    private AppUpdate? readyUpdate;
    private string? readyInstaller;
    private bool updateBusy;
    private bool installingUpdate;

    internal static ProcessStartInfo? PendingUpdateInstaller { get; private set; }

    private static bool? InstalledForAllUsers()
    {
        const string key = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\{D6F08F2C-06AF-4F6D-91A0-BA6BD1BC912D}_is1";
        foreach (var hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            using var root = RegistryKey.OpenBaseKey(hive, view);
            using var entry = root.OpenSubKey(key);
            if (entry?.GetValue("InstallLocation") is string folder &&
                string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder)),
                    Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory), StringComparison.OrdinalIgnoreCase))
                return hive == RegistryHive.LocalMachine;
        }
        return null;
    }

    private void OpenSettings()
    {
        var wasAutoUpdate = settings.AutoUpdateEnabled;
        using var dialog = new SettingsForm(settings);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        settings.ExportFolder = dialog.ExportFolder;
        settings.RememberLastExportFolder = dialog.RememberLastExportFolder;
        settings.AutoUpdateEnabled = dialog.AutoUpdateEnabled;
        AppSettingsStore.Save(settings);
        if (!settings.AutoUpdateEnabled) updateOperation?.Cancel();
        else if (!wasAutoUpdate) _ = CheckForUpdatesAsync(manual: false);
    }

    private async Task CheckForUpdatesAsync(bool manual)
    {
        if (updateBusy || installingUpdate || IsDisposed || !settings.AutoUpdateEnabled && !manual) return;
        if (readyUpdate is not null) { installUpdateButton.Visible = true; installUpdateButton.Enabled = true; return; }

        updateBusy = true;
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(updateLifetime.Token);
        updateOperation = operation;
        cancelUpdateButton.Visible = true;
        try
        {
            status.Text = "Проверяем обновления…";
            using var http = UpdateService.CreateClient();
            var service = new UpdateService(http);
            var update = await service.CheckAsync(Assembly.GetExecutingAssembly().GetName().Version!, operation.Token);
            if (update is null) { status.Text = "Установлена последняя версия."; return; }
            if (InstalledForAllUsers() is null)
            {
                status.Text = $"Доступна версия {update.Version}; эту копию нужно обновить установщиком вручную.";
                return;
            }
            status.Text = $"Загрузка обновления {update.Version}: 0%";
            var progress = new Progress<int>(percent =>
            {
                if (updateBusy && !IsDisposed && !Disposing)
                    status.Text = $"Загрузка обновления {update.Version}: {percent}%";
            });
            readyInstaller = await service.DownloadAsync(update, AppPaths.UpdatesDirectory, progress, operation.Token);
            readyUpdate = update;
            installUpdateButton.Visible = true;
            installUpdateButton.Enabled = true;
            status.Text = $"Версия {update.Version} загружена и проверена.";
        }
        catch (OperationCanceledException)
        {
            if (!updateLifetime.IsCancellationRequested && !IsDisposed)
                status.Text = "Загрузка обновления отменена.";
        }
        catch (Exception ex)
        {
            CrashLogger.Write(ex, "Update check/download");
            if (!IsDisposed) status.Text = manual ? "Ошибка проверки обновления: " + ex.Message : "Обновление сейчас недоступно.";
        }
        finally
        {
            updateOperation = null;
            updateBusy = false;
            if (!IsDisposed && !Disposing)
            {
                cancelUpdateButton.Visible = false;
                ResumeCloseAfterOperation();
            }
        }
    }

    private async Task InstallUpdateAsync()
    {
        if (installingUpdate || readyUpdate is null || readyInstaller is null || updateOperation is not null) return;
        installingUpdate = true;
        installUpdateButton.Enabled = false;
        try
        {
            var allUsers = InstalledForAllUsers() ?? throw new InvalidOperationException("Не найдена установленная копия приложения.");
            if (!await UpdateService.VerifyAsync(readyInstaller, readyUpdate, updateLifetime.Token))
            {
                readyUpdate = null;
                readyInstaller = null;
                installUpdateButton.Visible = false;
                throw new InvalidDataException("Установщик изменился или удалён. Проверьте обновления повторно.");
            }
            if (!workspace.Points.SequenceEqual(savedPoints) && !SaveCsv())
            {
                status.Text = "Обновление отменено. Сначала сохраните точки в CSV.";
                return;
            }

            var start = new ProcessStartInfo(readyInstaller) { UseShellExecute = true };
            foreach (var arg in new[] { "/SP-", "/SILENT", "/NORESTART", "/NOFORCECLOSEAPPLICATIONS", "/RESTARTAPP=1", allUsers ? "/ALLUSERS" : "/CURRENTUSER", "/DIR=" + Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory) })
                start.ArgumentList.Add(arg);
            PendingUpdateInstaller = start;
            Close();
        }
        catch (Exception ex)
        {
            PendingUpdateInstaller = null;
            if (!IsDisposed) { status.Text = "Обновление не установлено: " + ex.Message; installUpdateButton.Enabled = true; }
        }
        finally { installingUpdate = false; }
    }
}
