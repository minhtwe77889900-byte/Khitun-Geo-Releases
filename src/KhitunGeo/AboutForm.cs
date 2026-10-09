using System.Reflection;

namespace KhitunGeo;

internal sealed class AboutForm : Form
{
    public AboutForm()
    {
        Text = "О программе — Khitun Geo";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(590, 420);
        MinimumSize = new Size(560, 420);
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        BackColor = Color.White;
        Font = new Font("Segoe UI", 9F);

        var logoPath = Path.Combine(AppContext.BaseDirectory, "wwwroot", "brand", "khitun_geo_logo.png");
        var logo = new PictureBox
        {
            Dock = DockStyle.Top,
            Height = 145,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.White
        };
        if (File.Exists(logoPath))
        {
            try { logo.Image = Image.FromFile(logoPath); } catch { }
        }

        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.7.9";
        var integrityStatus = IntegrityVerifier.GetStatusText(AppContext.BaseDirectory);

        var details = new Label
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(28, 10, 28, 10),
            TextAlign = ContentAlignment.TopLeft,
            Font = new Font("Segoe UI", 10F),
            Text =
                $"Khitun Geo {version}\n" +
                "Офлайн геодезическое рабочее место для Windows\n\n" +
                "Издатель: Khitun Ivan\n" +
                "Платформа: .NET 8 / Windows Forms\n\n" +
                $"Проверка файлов: {integrityStatus}\n\n" +
                "Камера, микрофон, геолокация и уведомления приложению не требуются.\n" +
                "Точки сохраняются в выбранный пользователем файл CSV."
        };

        var copy = new Button { Text = "Копировать сведения", AutoSize = true };
        copy.Click += (_, _) =>
        {
            try
            {
                Clipboard.SetText(
                    $"Khitun Geo {version}\r\n" +
                    "Publisher: Khitun Ivan\r\n" +
                    $"Windows: {Environment.OSVersion}\r\n" +
                    $".NET: {Environment.Version}\r\n" +
                    "Платформа: .NET 8 / Windows Forms\r\n" +
                    $"Integrity: {integrityStatus}\r\n" +
                    $"Data: {AppPaths.AppDataDirectory}");
            }
            catch { }
        };

        var close = new Button { Text = "Закрыть", DialogResult = DialogResult.OK, AutoSize = true };
        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 52,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(12, 8, 12, 8),
            BackColor = Color.FromArgb(246, 249, 252)
        };
        buttons.Controls.Add(close);
        buttons.Controls.Add(copy);

        Controls.Add(details);
        Controls.Add(buttons);
        Controls.Add(logo);
        AcceptButton = close;
        CancelButton = close;
    }
}
