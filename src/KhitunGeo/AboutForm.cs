using System.Reflection;
using System.Diagnostics;

namespace KhitunGeo;

internal sealed class AboutForm : Form
{
    public AboutForm(bool dark = false)
    {
        Text = "О программе — Khitun Geo";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(640, 510);
        MinimumSize = new Size(640, 510);
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        BackColor = Color.White;
        Font = new Font("Segoe UI", 9F);

        var logo = new PictureBox
        {
            Dock = DockStyle.Top,
            Height = 145,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.White
        };
        using (var source = typeof(AboutForm).Assembly.GetManifestResourceStream("KhitunGeo.BrandMark.png"))
            if (source is not null) { using var image = Image.FromStream(source); logo.Image = new Bitmap(image); }
        logo.Disposed += (_, _) => logo.Image?.Dispose();

        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.7.11";
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
                    $"Data: {AppPaths.AppDataDirectory}\r\n" +
                    "GitHub: https://github.com/" + UpdateService.Repository + "\r\n" +
                    "Email: Vaniahit46@gmail.com");
            }
            catch { }
        };

        const string publicRepository = "https://github.com/" + UpdateService.Repository;
        const string email = "Vaniahit46@gmail.com";
        var contacts = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 85, FlowDirection = FlowDirection.TopDown,
            WrapContents = false, Padding = new Padding(28, 4, 12, 4) };
        var github = new LinkLabel { Name = "PublicGitHub", Text = "GitHub: " + publicRepository, AutoSize = true };
        github.LinkClicked += (_, _) => OpenContact(publicRepository);
        var mail = new LinkLabel { Name = "ContactEmail", Text = email, AutoSize = true };
        mail.LinkClicked += (_, _) => OpenContact("mailto:" + email);
        var copyEmail = new Button { Name = "CopyContactEmail", Text = "Копировать почту", AutoSize = true };
        copyEmail.Click += (_, _) => { try { Clipboard.SetText(email); } catch (Exception ex) { MessageBox.Show(this, ex.Message, "Копирование почты"); } };
        var emailRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
        emailRow.Controls.Add(mail); emailRow.Controls.Add(copyEmail);
        contacts.Controls.Add(github); contacts.Controls.Add(emailRow);

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
        Controls.Add(contacts);
        Controls.Add(buttons);
        Controls.Add(logo);
        AcceptButton = close;
        CancelButton = close;
        Native.NativeTheme.Apply(this, dark);
    }
    private void OpenContact(string uri)
    {
        try { Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true }); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Открытие ссылки"); }
    }
}
