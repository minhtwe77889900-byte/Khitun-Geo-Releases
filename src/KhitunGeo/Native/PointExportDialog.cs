namespace KhitunGeo.Native;

internal sealed class PointExportDialog : Form
{
    private readonly IReadOnlyList<SurveyPoint> points;
    private readonly bool geographic;
    private readonly string coordinateSystem;
    private readonly ComboBox format = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260 };
    private readonly ComboBox delimiter = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 };
    private readonly NumericUpDown decimals = new() { Minimum = 0, Maximum = 9, Value = 3, Width = 65 };
    private readonly CheckBox header = new() { Text = "Заголовок", AutoSize = true };
    private readonly Label notice = new() { Dock = DockStyle.Fill, AutoSize = false };
    private readonly DataGridView preview = new()
    {
        Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
        RowHeadersVisible = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
    };
    private readonly Button save = new() { Text = "Сохранить файл", AutoSize = true };
    private static readonly PointExportFormat[] Formats = {
        PointExportFormat.AutoCad, PointExportFormat.AutoCadScript, PointExportFormat.ExcelXlsx, PointExportFormat.Pnezd, PointExportFormat.Penzd,
        PointExportFormat.Nez, PointExportFormat.Enz, PointExportFormat.Xyz, PointExportFormat.Blh
    };
    private static readonly string[] Delimiters = { ",", ";", "\t", " " };
    public int ExportedCount { get; private set; }

    public PointExportDialog(IReadOnlyList<SurveyPoint> points, bool geographic, string sourceName, bool dark)
    {
        this.points = points.ToArray(); this.geographic = geographic; this.coordinateSystem = sourceName;
        Text = "Экспорт всех точек — " + sourceName;
        Size = new Size(840, 500); MinimumSize = new Size(700, 400);
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Segoe UI", 10);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, Padding = new Padding(10) };
        layout.RowStyles.Add(new(SizeType.Absolute, 70)); layout.RowStyles.Add(new(SizeType.Percent, 100)); layout.RowStyles.Add(new(SizeType.Absolute, 65));
        var options = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true };
        format.Items.AddRange(new object[] { "AutoCAD — X,Y,Z", "AutoCAD — скрипт POINT (.scr)", "Excel — книга (.xlsx)", "Civil 3D — PNEZD", "Civil 3D — PENZD", "Civil 3D — NEZ", "Civil 3D — ENZ", "Геодезические — X,Y,Z", "Географические — B,L,H" });
        delimiter.Items.AddRange(new object[] { "Запятая", "Точка с запятой", "Табуляция", "Пробел" });
        format.SelectedIndex = geographic ? Formats.Length - 1 : 0; delimiter.SelectedIndex = geographic ? 0 : 2;
        decimals.Value = geographic ? 9 : 3;
        options.Controls.Add(format); options.Controls.Add(delimiter);
        options.Controls.Add(new Label { Text = "Знаков", AutoSize = true, Margin = new Padding(5, 7, 3, 0) });
        options.Controls.Add(decimals); options.Controls.Add(header);
        var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        footer.ColumnStyles.Add(new(SizeType.Percent, 100)); footer.ColumnStyles.Add(new(SizeType.AutoSize));
        footer.Controls.Add(notice, 0, 0);
        var actions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
        var cancel = new Button { Text = "Отмена", AutoSize = true, DialogResult = DialogResult.Cancel };
        actions.Controls.Add(save); actions.Controls.Add(cancel); footer.Controls.Add(actions, 1, 0);
        layout.Controls.Add(options, 0, 0); layout.Controls.Add(preview, 0, 1); layout.Controls.Add(footer, 0, 2);
        Controls.Add(layout); CancelButton = cancel;
        format.SelectedIndexChanged += (_, _) =>
        {
            decimals.Value = Formats[format.SelectedIndex] is PointExportFormat.Blh or PointExportFormat.ExcelXlsx ? 9 : 3;
            delimiter.SelectedIndex = Formats[format.SelectedIndex] == PointExportFormat.AutoCad ? 2 : 0;
            var specialFormat = Formats[format.SelectedIndex] is PointExportFormat.AutoCadScript or PointExportFormat.ExcelXlsx;
            delimiter.Visible = header.Visible = !specialFormat;
            RefreshPreview();
        };
        delimiter.SelectedIndexChanged += (_, _) => RefreshPreview();
        decimals.ValueChanged += (_, _) => RefreshPreview(); header.CheckedChanged += (_, _) => RefreshPreview();
        save.Click += (_, _) => SaveFile();
        NativeTheme.Apply(this, dark); RefreshPreview();
    }

    private PointExportOptions Options() => new(Formats[format.SelectedIndex], Delimiters[delimiter.SelectedIndex], (int)decimals.Value,
        Formats[format.SelectedIndex] is not (PointExportFormat.AutoCadScript or PointExportFormat.ExcelXlsx) && header.Checked);

    private void RefreshPreview()
    {
        preview.Rows.Clear(); preview.Columns.Clear();
        try
        {
            var result = PointExport.Create(points, Options(), geographic);
            foreach (var column in result.Columns) preview.Columns.Add(column, column);
            foreach (var row in result.Rows.Take(10)) preview.Rows.Add(row.Cast<object>().ToArray());
            notice.Text = $"Точек: {result.Rows.Count}. Порядок: {string.Join(", ", result.Columns)}. Показаны первые 10.\nПустая высота экспортируется как 0.";
            if (Formats[format.SelectedIndex] == PointExportFormat.AutoCadScript)
                notice.Text = $"Точек: {result.Rows.Count}. Файл создаёт объекты POINT в мировой СК. В AutoCAD запустите команду SCRIPT и выберите сохранённый .scr.";
            if (Formats[format.SelectedIndex] == PointExportFormat.ExcelXlsx)
                notice.Text = $"Точек: {result.Rows.Count}. В книгу попадут текущие X/Y/Z, описание и СК: {coordinateSystem}. Пустая Z останется пустой.";
            save.Enabled = true;
        }
        catch (ArgumentException ex) { notice.Text = ex.Message; save.Enabled = false; }
    }

    private void SaveFile()
    {
        var options = Options();
        var script = options.Format == PointExportFormat.AutoCadScript;
        var excel = options.Format == PointExportFormat.ExcelXlsx;
        var csv = !script && !excel && options.Delimiter is "," or ";";
        using var dialog = new SaveFileDialog
        {
            Filter = script ? "Скрипт AutoCAD|*.scr" : excel ? "Книга Excel|*.xlsx" : csv ? "CSV|*.csv" : "Текст|*.txt",
            DefaultExt = script ? "scr" : excel ? "xlsx" : csv ? "csv" : "txt",
            FileName = script ? "Khitun_Geo_POINTS" : excel ? "Khitun_Geo_Points" : "Khitun_Geo_" + options.Format.ToString().ToUpperInvariant(),
            AddExtension = true, OverwritePrompt = true
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            PointExport.Save(dialog.FileName, points, options, geographic, coordinateSystem);
            ExportedCount = PointExport.Create(points, options, geographic).Rows.Count;
            DialogResult = DialogResult.OK; Close();
        }
        catch (Exception ex) when (ex is ArgumentException or System.IO.IOException or UnauthorizedAccessException)
        { MessageBox.Show(this, ex.Message, "Файл не сохранён"); }
    }
}
