namespace KhitunGeo;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using var instanceMutex = new Mutex(true, "Local\\KhitunGeo_" + Environment.UserName, out var isFirstInstance);
        if (!isFirstInstance)
        {
            MessageBox.Show("Khitun Geo уже запущен. Откройте существующее окно приложения.", "Khitun Geo");
            return;
        }
        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => ShowFatal(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex) ShowFatal(ex);
        };

        Application.Run(new Native.NativeWorkspaceForm());
        instanceMutex.ReleaseMutex();
        if (Native.NativeWorkspaceForm.PendingUpdateInstaller is { } installer)
        {
            try
            {
                if (System.Diagnostics.Process.Start(installer) is null)
                    throw new InvalidOperationException("Не удалось запустить установщик.");
            }
            catch (Exception ex)
            {
                CrashLogger.Write(ex, "Update installer launch");
                MessageBox.Show("Обновление не запущено. Текущая версия сохранена — откройте её снова и повторите попытку.\n\n" + ex.Message, "Khitun Geo");
            }
        }
    }

    private static void ShowFatal(Exception ex)
    {
        var log = CrashLogger.Write(ex, "Unhandled fatal exception");
        var logText = string.IsNullOrWhiteSpace(log)
            ? string.Empty
            : $"\n\nЖурнал: {log}";
        MessageBox.Show(
            $"Khitun Geo столкнулся с ошибкой:\n\n{ex.Message}{logText}\n\nПри следующем запуске приложение предложит восстановление, если доступна резервная копия.",
            "Khitun Geo",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }
}
