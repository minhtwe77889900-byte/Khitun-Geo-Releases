namespace KhitunGeo.Native;

internal sealed partial class NativeWorkspaceForm : Form
{
    private readonly PointWorkspace workspace = new();
    private readonly PointGrid grid = new();
    private readonly PointPreview preview = new();
    private readonly SplitContainer split = new();
    private readonly ToolStripStatusLabel status = new();
    private readonly CoordinateSelectionPanel conversion;
    private EmbeddedCoordinateRuntime? coordinateRuntime;
    private CancellationTokenSource? conversionCancellation;
    private bool dark;
    private bool importingDrawing;
    private DrawingScene? drawing;
    private SurveyPoint[] savedPoints = Array.Empty<SurveyPoint>();
    private readonly AppSettings settings = AppSettingsStore.Load();
    private Button? visualizationHeaderButton;
    private Button? visualizationSidebarButton;

    public NativeWorkspaceForm()
    {
        Text = "Khitun Geo";
        Width = 1280;
        Height = 800;
        MinimumSize = new Size(800, 500);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 10);

        var sidebar = new FlowLayoutPanel { Dock = DockStyle.Left, Width = 200, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(8) };
        sidebar.Controls.Add(new Label { Text = "РАБОЧЕЕ МЕСТО", Width = 178, Height = 28, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.DimGray, Font = new Font(Font, FontStyle.Bold) });
        AddButton(sidebar, "Точки и таблица", () => grid.Focus());
        sidebar.Controls.Add(new Label { Text = "ЧЕРТЁЖ", Width = 178, Height = 28, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.DimGray, Font = new Font(Font, FontStyle.Bold) });
        AddButton(sidebar, "Открыть DWG / DXF", async () => await ImportDrawing());
        AddButton(sidebar, "Слои и видимость", ShowDrawingLayers);
        AddButton(sidebar, "Убрать чертёж", ClearDrawing);
        sidebar.Controls.Add(new Label { Text = "ОБРАБОТКА", Width = 178, Height = 28, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.DimGray, Font = new Font(Font, FontStyle.Bold) });
        AddButton(sidebar, "Добавить точку", () =>
        {
            if (grid.EndEdit()) workspace.ReplacePoints(workspace.Points.Append(new SurveyPoint((workspace.Points.Count + 1).ToString(), null, null, null, "")).ToArray());
        });
        AddButton(sidebar, "Изменить высоты Z", OffsetHeight);
        AddButton(sidebar, "Отменить действие", () => { if (grid.EndEdit()) workspace.Undo(); });
        sidebar.Controls.Add(new Label { Text = "ВИД", Width = 178, Height = 28, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.DimGray, Font = new Font(Font, FontStyle.Bold) });
        visualizationSidebarButton = AddButton(sidebar, "Скрыть визуализацию", ToggleVisualization);
        sidebar.Controls.Add(new Label { Text = "СЕРВИС", Width = 178, Height = 28, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.DimGray, Font = new Font(Font, FontStyle.Bold) });
        sidebar.Controls.Add(passportButton);
        passportButton.Click += async (_, _) => await ExportPassportPdf();
        AddButton(sidebar, "Светлая / тёмная", () => { dark = !dark; NativeTheme.Apply(this, dark); });
        AddButton(sidebar, "Настройки", OpenSettings);
        AddButton(sidebar, "Проверить обновления", async () => await CheckForUpdatesAsync(manual: true));
        installUpdateButton.Click += async (_, _) => await InstallUpdateAsync();
        sidebar.Controls.Add(installUpdateButton);
        cancelUpdateButton.Click += (_, _) => updateOperation?.Cancel();
        sidebar.Controls.Add(cancelUpdateButton);
        AddButton(sidebar, "О программе", () => { using var about = new AboutForm(); about.ShowDialog(this); });
        AddButton(sidebar, "Диагностика", () => { using var diagnostics = new DiagnosticsForm(); diagnostics.ShowDialog(this); });

        conversion = new CoordinateSelectionPanel(
            Path.Combine(AppContext.BaseDirectory, "wwwroot", "crs", "native-systems.json"),
            () => { if (grid.EndEdit()) workspace.SwapXY(); });
        conversion.ConvertRequested += async (_, _) => await ConvertCoordinates();
        try
        {
            IntegrityVerifier.VerifyOrThrow(AppContext.BaseDirectory);
            var scripts = Path.Combine(AppContext.BaseDirectory, "wwwroot");
            coordinateRuntime = new EmbeddedCoordinateRuntime(
                File.ReadAllText(Path.Combine(scripts, "coordinate-core.js")),
                File.ReadAllText(Path.Combine(scripts, "coordinate-adapter.js")));
            qualityRuntime = new NativeQualityRuntime(File.ReadAllText(Path.Combine(scripts, "workspace-core.js")));
            conversion.SetRuntimeReady(true);
        }
        catch (Exception ex) { conversion.SetRuntimeReady(false, ex.Message); }

        split.Dock = DockStyle.Fill;
        split.Size = new Size(1100, 600);
        split.Panel1MinSize = 320;
        split.Panel2MinSize = 200;
        split.SplitterDistance = 550;
        split.Panel1.Controls.Add(grid);
        split.Panel2.Controls.Add(BuildPreviewHost());
        var footer = new StatusStrip();
        footer.Items.Add(status);
        footer.Items.Add(cancelPdf);
        cancelPdf.Click += (_, _) => pdfCancellation?.Cancel();
        var content = new Panel { Dock = DockStyle.Fill };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Margin = Padding.Empty };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 150));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 27));
        layout.Controls.Add(BuildApplicationHeader(sidebar), 0, 0);
        conversion.Dock = DockStyle.Fill;
        layout.Controls.Add(conversion, 0, 1);
        layout.Controls.Add(split, 0, 2);
        layout.Controls.Add(footer, 0, 3);
        content.Controls.Add(layout);
        Controls.Add(content);
        Controls.Add(sidebar);
        grid.Bind(workspace);
        preview.DrawingStatusChanged += (_, _) => RefreshStatus();
        workspace.Changed += WorkspaceChanged;
        WorkspaceChanged(this, EventArgs.Empty);
        NativeTheme.Apply(this, false);
        AllowDrop = true;
        DragEnter += (_, e) =>
        {
            if (e.Data?.GetData(DataFormats.FileDrop) is string[] paths && paths.All(IsSupportedDataFile))
                e.Effect = DragDropEffects.Copy;
        };
        DragDrop += (_, e) =>
        {
            if (e.Data?.GetData(DataFormats.FileDrop) is string[] paths) ImportFiles(paths);
        };
        FormClosing += (_, e) =>
        {
            if (updateOperation is not null)
            {
                pendingClose.Request(); updateOperation.Cancel(); e.Cancel = true;
                status.Text = "Остановка проверки обновлений перед закрытием…";
                return;
            }
            if (pdfCancellation is not null)
            {
                pendingClose.Request();
                pdfCancellation.Cancel(); e.Cancel = true;
                status.Text = "Остановка экспорта PDF перед закрытием…";
                return;
            }
            if (conversionCancellation is not null)
            {
                pendingClose.Request();
                conversionCancellation.Cancel(); e.Cancel = true;
                status.Text = "Остановка расчёта перед закрытием…";
                return;
            }
            if (importingDrawing) { pendingClose.Request(); e.Cancel = true; return; }
            if (!grid.EndEdit()) { e.Cancel = true; return; }
            if (workspace.Points.SequenceEqual(savedPoints)) return;
            var answer = MessageBox.Show(this, "Сохранить текущие точки в CSV перед закрытием?", "Khitun Geo", MessageBoxButtons.YesNoCancel);
            e.Cancel = answer == DialogResult.Cancel || (answer == DialogResult.Yes && !SaveCsv());
        };
        Shown += (_, _) => { if (settings.AutoUpdateEnabled) _ = CheckForUpdatesAsync(manual: false); };
        FormClosed += (_, _) => updateLifetime.Cancel();
    }

    private static Button AddButton(Control parent, string text, Action action)
    {
        var button = new Button { Text = text, Width = 178, Height = 34, FlatStyle = FlatStyle.Flat, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(8, 0, 0, 0) };
        button.Click += (_, _) => action();
        parent.Controls.Add(button);
        return button;
    }

    private Control BuildApplicationHeader(FlowLayoutPanel sidebar)
    {
        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 7, RowCount = 1, Padding = new Padding(8, 5, 10, 5), BackColor = Color.White };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 36));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 38));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 94));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 88));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 88));
        var toggle = new Button { Text = "☰", Dock = DockStyle.Fill, FlatStyle = FlatStyle.Flat, Font = new Font(Font.FontFamily, 15), AccessibleName = "Показать или скрыть левую панель" };
        toggle.Click += (_, _) => sidebar.Visible = !sidebar.Visible;
        var logo = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, Margin = new Padding(2) };
        var logoPath = Path.Combine(AppContext.BaseDirectory, "wwwroot", "brand", "khitun_geo_icon_64.png");
        if (File.Exists(logoPath)) { using var source = Image.FromFile(logoPath); logo.Image = new Bitmap(source); }
        var identity = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = new Padding(4, 2, 0, 0) };
        identity.RowStyles.Add(new RowStyle(SizeType.Percent, 58)); identity.RowStyles.Add(new RowStyle(SizeType.Percent, 42));
        identity.Controls.Add(new Label { Text = "Khitun Geo", Dock = DockStyle.Fill, Font = new Font(Font, FontStyle.Bold), TextAlign = ContentAlignment.BottomLeft }, 0, 0);
        identity.Controls.Add(new Label { Text = "Геодезическая рабочая среда", Dock = DockStyle.Fill, Font = new Font(Font.FontFamily, 8.5f), ForeColor = Color.DimGray, TextAlign = ContentAlignment.TopLeft }, 0, 1);
        var import = new Button { Text = "↑  Импорт", Dock = DockStyle.Fill, BackColor = Color.FromArgb(8, 127, 115), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Margin = new Padding(3) };
        var export = new Button { Text = "↓  Экспорт", Dock = DockStyle.Fill, FlatStyle = FlatStyle.Flat, Margin = new Padding(3) };
        visualizationHeaderButton = new Button { Text = "Вид: скрыть", Dock = DockStyle.Fill, FlatStyle = FlatStyle.Flat, Margin = new Padding(3) };
        import.Click += (_, _) => ImportText(); export.Click += (_, _) => ExportPoints();
        import.Text = "Импорт";
        export.Text = "Экспорт";
        visualizationHeaderButton.Click += (_, _) => ToggleVisualization();
        header.Controls.Add(toggle, 0, 0); header.Controls.Add(logo, 1, 0); header.Controls.Add(identity, 2, 0);
        header.Controls.Add(new Panel { Dock = DockStyle.Fill }, 3, 0);
        header.Controls.Add(visualizationHeaderButton, 4, 0); header.Controls.Add(import, 5, 0); header.Controls.Add(export, 6, 0);
        return header;
    }

    private Control BuildPreviewHost()
    {
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, Padding = new Padding(4, 2, 4, 2) };
        var hide = new Button { Text = "Скрыть", Width = 72, Height = 28, FlatStyle = FlatStyle.Flat };
        var fit = new Button { Text = "Вписать", Width = 72, Height = 28, FlatStyle = FlatStyle.Flat };
        var zoom = new Label { Text = "100%", Width = 48, Height = 28, TextAlign = ContentAlignment.MiddleCenter };
        var plus = new Button { Text = "+", Width = 30, Height = 28, FlatStyle = FlatStyle.Flat };
        var minus = new Button { Text = "−", Width = 30, Height = 28, FlatStyle = FlatStyle.Flat };
        var title = new Label { Text = "Визуализация", AutoSize = true, Dock = DockStyle.Left, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(8, 6, 0, 0) };
        hide.Click += (_, _) => ToggleVisualization(); fit.Click += (_, _) => preview.FitToContent();
        plus.Click += (_, _) => preview.ZoomBy(1.2); minus.Click += (_, _) => preview.ZoomBy(1 / 1.2);
        preview.ZoomChanged += (_, _) => zoom.Text = $"{preview.ZoomPercent}%";
        toolbar.Controls.Add(hide); toolbar.Controls.Add(fit); toolbar.Controls.Add(zoom); toolbar.Controls.Add(plus); toolbar.Controls.Add(minus); toolbar.Controls.Add(title);
        layout.Controls.Add(toolbar, 0, 0); layout.Controls.Add(preview, 0, 1);
        return layout;
    }

    private void ToggleVisualization()
    {
        var show = split.Panel2Collapsed;
        preview.SetActive(show);
        split.Panel2Collapsed = !show;
        var text = show ? "Вид: скрыть" : "Вид: показать";
        if (visualizationHeaderButton is not null) visualizationHeaderButton.Text = text;
        if (visualizationSidebarButton is not null) visualizationSidebarButton.Text = show ? "Скрыть визуализацию" : "Показать визуализацию";
        RefreshStatus();
    }

    private void WorkspaceChanged(object? sender, EventArgs e)
    {
        preview.SetPoints(workspace.Points);
        RefreshStatus();
    }

    private void RefreshStatus()
    {
        status.Text = $"Точек: {workspace.Points.Count}  |  Элементов чертежа: {drawing?.StoredElementCount ?? 0}";
        if (preview.Truncated) status.Text += "  |  Чертёж показан частично (ограничение отрисовки)";
        if (preview.DetailLimited) status.Text += "  |  Детализация дуг ограничена";
        if (conversionCancellation is not null) status.Text += "  |  Преобразование…";
        if (importingDrawing) status.Text += "  |  Чтение чертежа…";
        if (pdfCancellation is not null) status.Text += "  |  Создание PDF…";
        passportButton.Enabled = passportState.Current is not null && conversionCancellation is null && pdfCancellation is null;
    }

    private async Task ConvertCoordinates()
    {
        if (conversionCancellation is not null) { conversionCancellation.Cancel(); return; }
        if (pdfCancellation is not null || coordinateRuntime is null || qualityRuntime is null || !grid.EndEdit()) return;
        if (workspace.Points.Count == 0) { MessageBox.Show(this, "Добавьте точки для преобразования.", "Расчёт"); return; }
        using var cancellation = new CancellationTokenSource();
        try
        {
            var pointSnapshot = workspace.Points.ToArray();
            var operation = new PassportConversion(pointSnapshot, conversion.SettingsSnapshot(), conversion.SourceName,
                conversion.TargetName, Application.ProductVersion, DateTimeOffset.UtcNow);
            var geographic = conversion.SourceIsGeographic;
            var reportZones = conversion.UsesAutoTargetZone;
            var restoreSettings = conversion.CaptureUndo();
            conversionCancellation = cancellation;
            conversion.SetBusy(true); WorkspaceChanged(this, EventArgs.Empty);
            var runtime = coordinateRuntime;
            var checker = qualityRuntime;
            var result = await Task.Run(() =>
            {
                var issues = checker.Check(pointSnapshot, geographic, cancellation.Token);
                NativeQualityRuntime.EnsureNoErrors(issues);
                return (Response: runtime.Convert(operation.Request, cancellation.Token), Issues: issues);
            }, cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            if (IsDisposed || Disposing || !grid.EndEdit()) return;
            var zones = operation.Apply(workspace, conversion.SettingsSnapshot(), result.Response, result.Issues, passportState, restoreSettings);
            conversion.AcceptResult(zones);
            if (reportZones && zones.Count > 0)
                MessageBox.Show(this, "Автоматически выбраны зоны: " + string.Join(", ", zones), "Преобразование выполнено");
        }
        catch (OperationCanceledException) { if (!IsDisposed) status.Text = "Расчёт прерван. Точки сохранены."; }
        catch (Exception ex) { if (!IsDisposed) MessageBox.Show(this, ex.Message, "Преобразование не выполнено"); }
        finally
        {
            conversionCancellation = null;
            if (!IsDisposed && !Disposing) { conversion.SetBusy(false); WorkspaceChanged(this, EventArgs.Empty); ResumeCloseAfterOperation(); }
        }
    }

    private void ImportText()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "Все поддерживаемые данные|*.xlsx;*.csv;*.tsv;*.txt;*.xyz;*.pnt;*.dat;*.asc;*.json;*.geojson|Книга Excel (*.xlsx)|*.xlsx|JSON и GeoJSON (*.json;*.geojson)|*.json;*.geojson|CSV и TSV (*.csv;*.tsv)|*.csv;*.tsv|Текст и координатные файлы (*.txt;*.xyz;*.pnt;*.dat;*.asc)|*.txt;*.xyz;*.pnt;*.dat;*.asc|Все файлы (*.*)|*.*",
            FilterIndex = 1, Multiselect = true, CheckFileExists = true
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        ImportFiles(dialog.FileNames);
    }

    private static bool IsSupportedDataFile(string path) => Path.GetExtension(path).ToLowerInvariant() is ".xlsx" or ".csv" or ".tsv" or ".txt" or ".xyz" or ".pnt" or ".dat" or ".asc" or ".json" or ".geojson";

    private void ImportFiles(string[] paths)
    {
        if (!grid.EndEdit()) return;
        foreach (var path in paths)
        {
            try
            {
                using var wizard = new PointImportWizardForm(path, workspace.Points.Count);
                if (wizard.ShowDialog(this) != DialogResult.OK) break;
                var next = wizard.AppendToCurrent
                    ? workspace.Points.Concat(wizard.ImportedPoints).ToArray()
                    : wizard.ImportedPoints.ToArray();
                workspace.ReplacePoints(next);
                preview.FitToContent();
                if (wizard.SourceCrsId is string sourceId && conversion.SelectSource(sourceId))
                    status.Text = $"Импортировано точек: {wizard.ImportedPoints.Count}. Исходная СК из файла: {conversion.SourceName}.";
                else status.Text = $"Импортировано точек: {wizard.ImportedPoints.Count}.";
            }
            catch (Exception ex)
            { MessageBox.Show(this, ex.Message, "Мастер импорта"); }
        }
    }

    private void ExportPoints()
    {
        if (conversionCancellation is not null || !grid.EndEdit()) return;
        try
        {
            using var dialog = new PointExportDialog(workspace.Points, conversion.SourceIsGeographic, conversion.SourceName, dark);
            if (dialog.ShowDialog(this) == DialogResult.OK)
                status.Text = $"Экспортировано точек: {dialog.ExportedCount}.";
        }
        catch (InvalidOperationException ex) { MessageBox.Show(this, ex.Message, "Экспорт точек"); }
    }

    private bool SaveCsv()
    {
        if (!grid.EndEdit()) return false;
        using var dialog = new SaveFileDialog
        {
            Filter = "CSV|*.csv", DefaultExt = "csv", FileName = "points.csv", AddExtension = true,
            OverwritePrompt = true,
            InitialDirectory = settings.RememberLastExportFolder && Directory.Exists(settings.ExportFolder)
                ? settings.ExportFolder : AppPaths.DefaultExportDirectory
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return false;
        try
        {
            PointFileService.SaveCsv(dialog.FileName, workspace.Points);
            savedPoints = workspace.Points.ToArray();
            if (settings.RememberLastExportFolder)
            {
                settings.ExportFolder = Path.GetDirectoryName(dialog.FileName) ?? AppPaths.DefaultExportDirectory;
                AppSettingsStore.Save(settings);
            }
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        { MessageBox.Show(this, ex.Message, "Экспорт CSV"); return false; }
    }

    private void OffsetHeight()
    {
        if (!grid.EndEdit()) return;
        using var dialog = new Form { Text = "Изменить высоты всех точек", Width = 470, Height = 245, StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false };
        var operation = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        operation.Items.AddRange(new object[]
        {
            "Выберите действие…",
            "Прибавить значение к Z каждой точки",
            "Вычесть значение из Z каждой точки",
            "Задать одну абсолютную отметку всем точкам"
        });
        operation.SelectedIndex = 0;
        var valueLabel = new Label { Text = "Значение, м", AutoSize = true, Anchor = AnchorStyles.Left };
        var value = CreateHeightInput();
        var formula = new Label { AutoSize = true, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.DarkGreen };
        var fields = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 2, RowCount = 3 };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38)); fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62));
        fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 42)); fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 42)); fields.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        fields.Controls.Add(new Label { Text = "Операция", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0); fields.Controls.Add(operation, 1, 0);
        fields.Controls.Add(valueLabel, 0, 1); fields.Controls.Add(value, 1, 1);
        fields.Controls.Add(formula, 0, 2); fields.SetColumnSpan(formula, 2);
        var apply = new Button { Text = "Применить ко всем точкам", Dock = DockStyle.Bottom, Height = 38, DialogResult = DialogResult.None, Enabled = false };
        dialog.Controls.Add(fields);
        dialog.Controls.Add(apply);
        dialog.AcceptButton = apply;

        void UpdateFields()
        {
            var amount = (double)value.Value;
            valueLabel.Text = operation.SelectedIndex == 3 ? "Абсолютная отметка, м" : "Значение, м";
            formula.Text = operation.SelectedIndex switch
            {
                1 => $"Z новое = Z текущее + {amount:0.###} · применяется ко всем точкам с заданным Z",
                2 => $"Z новое = Z текущее − {amount:0.###} · применяется ко всем точкам с заданным Z",
                3 => $"Для каждой точки будет задано Z = {amount:0.###}",
                _ => "Выберите действие. Изменение можно отменить через Ctrl+Z."
            };
            apply.Enabled = operation.SelectedIndex > 0 && workspace.Points.Count > 0;
        }

        operation.SelectedIndexChanged += (_, _) => UpdateFields();
        value.ValueChanged += (_, _) => UpdateFields();
        UpdateFields();
        apply.Click += (_, _) =>
        {
            try
            {
                var mode = operation.SelectedIndex switch
                {
                    1 => HeightOperation.AddToAll,
                    2 => HeightOperation.SubtractFromAll,
                    3 => HeightOperation.SetAll,
                    _ => throw new InvalidOperationException("Выберите операцию высоты.")
                };
                workspace.ReplacePoints(HeightCalculator.Apply(workspace.Points, mode, (double)value.Value));
                dialog.DialogResult = DialogResult.OK;
                dialog.Close();
            }
            catch (ArgumentException ex)
            { MessageBox.Show(dialog, ex.Message, "Расчёт высоты"); }
        };
        dialog.ShowDialog(this);
    }

    private static NumericUpDown CreateHeightInput() => new()
    {
        DecimalPlaces = 3,
        Increment = 0.001m,
        Minimum = -1000000000,
        Maximum = 1000000000,
        Dock = DockStyle.Fill
    };

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            conversionCancellation?.Cancel();
            pdfCancellation?.Cancel();
            updateLifetime.Cancel();
            workspace.Changed -= WorkspaceChanged;
        }
        base.Dispose(disposing);
    }
}
