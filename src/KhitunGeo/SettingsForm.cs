namespace KhitunGeo;

internal sealed class SettingsForm : Form
{
    private readonly CheckBox autoUpdate = new()
    {
        Text = "Проверять и загружать обновления при запуске",
        AutoSize = true
    };
    private readonly TextBox exportFolder = new() { Dock = DockStyle.Fill, ReadOnly = true };
    private readonly CheckBox rememberFolder = new()
    {
        Text = "Запоминать последнюю папку экспорта",
        AutoSize = true
    };

    public bool AutoUpdateEnabled => autoUpdate.Checked;
    public bool RememberLastExportFolder => rememberFolder.Checked;
    public string ExportFolder => exportFolder.Text;

    public SettingsForm(AppSettings settings)
    {
        Text = "Настройки — Khitun Geo";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(560, 220);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        Font = new Font("Segoe UI", 9F);

        autoUpdate.Checked = settings.AutoUpdateEnabled;
        rememberFolder.Checked = settings.RememberLastExportFolder;
        exportFolder.Text = Directory.Exists(settings.ExportFolder) ? settings.ExportFolder : AppPaths.DefaultExportDirectory;

        var folderLabel = new Label { Text = "Папка экспорта", AutoSize = true, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
        var choose = new Button { Text = "Выбрать…", AutoSize = true };
        choose.Click += (_, _) => ChooseFolder();
        var folderRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        folderRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        folderRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        folderRow.Controls.Add(exportFolder, 0, 0);
        folderRow.Controls.Add(choose, 1, 0);

        var save = new Button { Text = "Сохранить", DialogResult = DialogResult.OK, AutoSize = true };
        var cancel = new Button { Text = "Отмена", DialogResult = DialogResult.Cancel, AutoSize = true };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 48, FlowDirection = FlowDirection.RightToLeft };
        buttons.Controls.Add(save);
        buttons.Controls.Add(cancel);

        var body = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 1, RowCount = 5 };
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        body.Controls.Add(folderLabel, 0, 0);
        body.Controls.Add(folderRow, 0, 1);
        body.Controls.Add(rememberFolder, 0, 2);
        body.Controls.Add(autoUpdate, 0, 3);
        body.Controls.Add(buttons, 0, 4);
        Controls.Add(body);
        AcceptButton = save;
        CancelButton = cancel;
    }

    private void ChooseFolder()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Выберите папку для экспорта Khitun Geo",
            UseDescriptionForTitle = true,
            SelectedPath = Directory.Exists(exportFolder.Text) ? exportFolder.Text : AppPaths.DefaultExportDirectory,
            ShowNewFolderButton = true
        };
        if (dialog.ShowDialog(this) == DialogResult.OK) exportFolder.Text = dialog.SelectedPath;
    }
}
