using System.Reflection;

namespace KhitunGeo;

internal sealed class DiagnosticsForm : Form
{
    private readonly TextBox details = new()
    {
        Dock = DockStyle.Fill,
        Multiline = true,
        ReadOnly = true,
        ScrollBars = ScrollBars.Vertical,
        Font = new Font("Consolas", 9F),
        BackColor = Color.White
    };

    public DiagnosticsForm()
    {
        Text = "Диагностика — Khitun Geo";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(720, 440);
        MinimumSize = new Size(640, 380);
        ShowInTaskbar = false;
        Font = new Font("Segoe UI", 9F);
        details.Text = BuildReport();

        var copy = new Button { Text = "Копировать отчёт", AutoSize = true };
        copy.Click += (_, _) => { try { Clipboard.SetText(details.Text); } catch { } };
        var logs = new Button { Text = "Журналы ошибок", AutoSize = true };
        logs.Click += (_, _) => OpenFolder(AppPaths.LogsDirectory);
        var close = new Button { Text = "Закрыть", DialogResult = DialogResult.OK, AutoSize = true };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 54, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(10, 9, 10, 7) };
        buttons.Controls.Add(close);
        buttons.Controls.Add(copy);
        buttons.Controls.Add(logs);
        Controls.Add(details);
        Controls.Add(buttons);
        AcceptButton = close;
        CancelButton = close;
    }

    private static string BuildReport()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "не определена";
        var latestLog = CrashLogger.GetLatestLog();
        var integrity = IntegrityVerifier.GetStatusText(AppContext.BaseDirectory);
        var drive = new DriveInfo(Path.GetPathRoot(AppPaths.AppDataDirectory) ?? "C:\\");
        var freeGb = drive.IsReady ? drive.AvailableFreeSpace / 1024d / 1024d / 1024d : -1;
        return
            $"Khitun Geo {version}\r\n" +
            "Платформа: .NET 8 / Windows Forms\r\n" +
            $"Время отчёта: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\r\n\r\n" +
            $"Windows: {Environment.OSVersion}\r\n" +
            $"64-bit OS: {Environment.Is64BitOperatingSystem}\r\n" +
            $"64-bit process: {Environment.Is64BitProcess}\r\n" +
            $".NET: {Environment.Version}\r\n" +
            $"Проверка файлов: {integrity}\r\n" +
            $"Свободно на диске данных: {(freeGb >= 0 ? freeGb.ToString("0.0") + " ГБ" : "не определено")}\r\n\r\n" +
            $"Папка данных: {AppPaths.AppDataDirectory}\r\n" +
            $"Последний журнал ошибки: {latestLog ?? "нет"}\r\n";
    }

    private static void OpenFolder(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
        }
        catch { }
    }
}
