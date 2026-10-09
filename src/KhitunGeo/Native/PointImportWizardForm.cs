namespace KhitunGeo.Native;

internal sealed class PointImportWizardForm : Form
{
    private readonly string path;
    private readonly int currentPointCount;
    private readonly ComboBox sheetPicker = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
    private readonly ComboBox delimiterPicker = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 130 };
    private readonly ComboBox modePicker = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 190 };
    private readonly CheckBox headerCheck = new() { Text = "Первая строка — заголовки", AutoSize = true };
    private readonly CheckBox swapCheck = new() { Text = "Поменять X и Y", AutoSize = true };
    private readonly CheckBox skipInvalidCheck = new() { Text = "Пропустить ошибочные строки", AutoSize = true };
    private readonly DataGridView previewGrid = new() { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, RowHeadersVisible = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells, SelectionMode = DataGridViewSelectionMode.FullRowSelect };
    private readonly Label summary = new() { Dock = DockStyle.Fill, AutoSize = false, TextAlign = ContentAlignment.MiddleLeft };
    private readonly Label fileLabel = new() { Dock = DockStyle.Fill, AutoSize = false, TextAlign = ContentAlignment.MiddleLeft };
    private readonly Button applyButton = new() { Text = "Загрузить точки", AutoSize = true, Enabled = false };
    private readonly ComboBox[] mappings = new ComboBox[5];
    private readonly string[] mappingNames = { "№ точки", "X / север / широта", "Y / восток / долгота", "Z / высота", "Описание" };
    private PointImportPreview preview;
    private PointImportReview review = new(Array.Empty<SurveyPoint>(), Array.Empty<string>());

    public IReadOnlyList<SurveyPoint> ImportedPoints => review.Points;
    public bool AppendToCurrent => modePicker.SelectedIndex != 1;
    public string? SourceCrsId => preview.SourceCrsId;

    public PointImportWizardForm(string path, int currentPointCount)
    {
        this.path = path;
        this.currentPointCount = currentPointCount;
        preview = PointImportService.Open(path);
        Text = "Мастер импорта точек";
        Width = 1040; Height = 720; MinimumSize = new Size(820, 560);
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Segoe UI", 9.5f);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = new Padding(12) };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 116));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
        fileLabel.Font = new Font(Font, FontStyle.Bold);
        layout.Controls.Add(fileLabel, 0, 0);
        layout.Controls.Add(BuildOptions(), 0, 1);
        layout.Controls.Add(BuildMappings(), 0, 2);
        layout.Controls.Add(previewGrid, 0, 3);
        layout.Controls.Add(BuildFooter(), 0, 4);
        Controls.Add(layout);
        AcceptButton = applyButton;

        foreach (var (mapping, index) in mappings.Select((control, index) => (control, index)))
            mapping.SelectedIndexChanged += (_, _) => RefreshReview();
        headerCheck.CheckedChanged += (_, _) => { ResetMappings(); RefreshPreview(); };
        swapCheck.CheckedChanged += (_, _) => RefreshReview();
        skipInvalidCheck.CheckedChanged += (_, _) => RefreshReview();
        modePicker.SelectedIndexChanged += (_, _) => RefreshReview();
        sheetPicker.SelectedIndexChanged += (_, _) => LoadSelectedSheet();
        delimiterPicker.SelectedIndexChanged += (_, _) => LoadSelectedDelimiter();
        applyButton.Click += (_, _) =>
        {
            if (review.Points.Count == 0 || review.Errors.Count > 0 && !skipInvalidCheck.Checked) return;
            DialogResult = DialogResult.OK;
            Close();
        };

        sheetPicker.Items.AddRange(preview.Sheets.Cast<object>().ToArray());
        if (preview.Sheets.Count > 0) sheetPicker.SelectedItem = preview.SheetName;
        delimiterPicker.Items.AddRange(new object[] { "Авто", "Точка с запятой ;", "Запятая ,", "Табуляция", "Пробелы" });
        delimiterPicker.SelectedIndex = 0;
        modePicker.Items.AddRange(new object[] { "Добавить к текущим", "Заменить текущие точки" });
        modePicker.SelectedIndex = currentPointCount == 0 ? 1 : 0;
        headerCheck.Checked = preview.HasHeader;
        ResetMappings();
        RefreshPreview();
    }

    private Control BuildOptions()
    {
        var options = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true, AutoScroll = true, Padding = new Padding(0, 5, 0, 0) };
        var isXlsx = Path.GetExtension(path).Equals(".xlsx", StringComparison.OrdinalIgnoreCase);
        var isText = Path.GetExtension(path).ToLowerInvariant() is ".csv" or ".tsv" or ".txt" or ".xyz" or ".pnt" or ".dat" or ".asc";
        sheetPicker.Visible = isXlsx;
        delimiterPicker.Visible = isText;
        options.Controls.Add(new Label { Text = "Лист Excel:", AutoSize = true, Visible = isXlsx, Margin = new Padding(0, 7, 4, 0) });
        options.Controls.Add(sheetPicker);
        options.Controls.Add(new Label { Text = "Разделитель:", AutoSize = true, Visible = isText, Margin = new Padding(10, 7, 4, 0) });
        options.Controls.Add(delimiterPicker);
        options.Controls.Add(modePicker);
        options.Controls.Add(headerCheck);
        options.Controls.Add(swapCheck);
        return options;
    }

    private Control BuildMappings()
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 5, RowCount = 2, Padding = new Padding(0, 6, 0, 4) };
        for (var i = 0; i < 5; i++)
        {
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
            mappings[i] = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, DropDownWidth = 220 };
            panel.Controls.Add(new Label { Text = mappingNames[i], AutoSize = true, Anchor = AnchorStyles.Left }, i, 0);
            panel.Controls.Add(mappings[i], i, 1);
        }
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        return panel;
    }

    private Control BuildFooter()
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2 };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        panel.Controls.Add(summary, 0, 0); panel.SetColumnSpan(summary, 2);
        panel.Controls.Add(skipInvalidCheck, 0, 1);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
        var cancel = new Button { Text = "Отмена", AutoSize = true, DialogResult = DialogResult.Cancel };
        actions.Controls.Add(applyButton); actions.Controls.Add(cancel);
        panel.Controls.Add(actions, 1, 1);
        return panel;
    }

    private void LoadSelectedSheet()
    {
        if (sheetPicker.SelectedItem is not string selected || selected == preview.SheetName) return;
        try { preview = PointImportService.Open(path, selected); headerCheck.Checked = preview.HasHeader; ResetMappings(); RefreshPreview(); }
        catch (Exception ex) { ShowLoadError(ex); }
    }

    private void LoadSelectedDelimiter()
    {
        if (Path.GetExtension(path).ToLowerInvariant() == ".xlsx" || Path.GetExtension(path).ToLowerInvariant() is ".json" or ".geojson") return;
        try { preview = PointImportService.Open(path, delimiter: SelectedDelimiter()); headerCheck.Checked = preview.HasHeader; ResetMappings(); RefreshPreview(); }
        catch (Exception ex) { ShowLoadError(ex); }
    }

    private string? SelectedDelimiter() => delimiterPicker.SelectedIndex switch
    {
        1 => ";", 2 => ",", 3 => "\t", 4 => "space", _ => null
    };

    private void ResetMappings()
    {
        if (mappings.Any(control => control is null)) return;
        var suggested = PointImportService.DetectMapping(preview, headerCheck.Checked);
        var values = new[] { suggested.Name, suggested.X, suggested.Y, suggested.Height, suggested.Description };
        for (var i = 0; i < mappings.Length; i++)
        {
            mappings[i].BeginUpdate(); mappings[i].Items.Clear(); mappings[i].Items.Add("Не использовать");
            for (var column = 0; column < preview.Columns.Length; column++) mappings[i].Items.Add($"{column + 1}: {preview.Columns[column]}");
            mappings[i].SelectedIndex = values[i] >= 0 && values[i] < preview.Columns.Length ? values[i] + 1 : 0;
            mappings[i].EndUpdate();
            mappings[i].Enabled = preview.Columns.Length > 0;
        }
    }

    private PointImportMapping SelectedMapping() => new(
        mappings[0].SelectedIndex - 1, mappings[1].SelectedIndex - 1, mappings[2].SelectedIndex - 1,
        mappings[3].SelectedIndex - 1, mappings[4].SelectedIndex - 1);

    private void RefreshPreview()
    {
        fileLabel.Text = preview.SourceCrsId is null
            ? $"{preview.FileName} — {preview.Format}"
            : $"{preview.FileName} — {preview.Format}; СК в файле: {preview.SourceCrsId}";
        previewGrid.Columns.Clear(); previewGrid.Rows.Clear();
        for (var i = 0; i < preview.Columns.Length; i++)
            previewGrid.Columns.Add("c" + i, preview.Columns[i]);
        var start = headerCheck.Checked && preview.Rows.Length > 0 ? 1 : 0;
        for (var i = start; i < Math.Min(preview.Rows.Length, start + 100); i++)
        {
            var row = preview.Rows[i];
            var values = Enumerable.Range(0, preview.Columns.Length).Select(column => (object)(column < row.Length ? row[column] : "")).ToArray();
            previewGrid.Rows.Add(values);
        }
        RefreshReview();
    }

    private void RefreshReview()
    {
        review = PointImportService.Map(preview, SelectedMapping(), headerCheck.Checked, swapCheck.Checked);
        if (review.InvalidRows is not null)
            foreach (var index in review.InvalidRows)
                if (index >= 0 && index < previewGrid.Rows.Count)
                    previewGrid.Rows[index].DefaultCellStyle.BackColor = Color.MistyRose;
        var total = Math.Max(0, preview.Rows.Length - (headerCheck.Checked && preview.Rows.Length > 0 ? 1 : 0));
        var problemCount = review.Errors.Count;
        summary.Text = $"Проверено строк: {total}  |  Готово к загрузке: {review.Points.Count}  |  Ошибок: {problemCount}" +
            (problemCount > 0 ? "  |  " + string.Join("; ", review.Errors.Take(2)) : "");
        summary.ForeColor = problemCount > 0 ? Color.DarkRed : Color.DarkGreen;
        applyButton.Enabled = review.Points.Count > 0 && (problemCount == 0 || skipInvalidCheck.Checked);
        applyButton.Text = AppendToCurrent ? "Добавить точки" : "Заменить точки";
    }

    private void ShowLoadError(Exception ex)
    {
        summary.Text = "Не удалось прочитать файл: " + ex.Message;
        summary.ForeColor = Color.DarkRed;
        applyButton.Enabled = false;
    }
}
