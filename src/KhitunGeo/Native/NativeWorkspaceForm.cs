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
        var screen = Screen.FromPoint(Cursor.Position).WorkingArea;
        Width = Math.Min(1280, screen.Width - 40);
        Height = Math.Min(800, screen.Height - 40);
        MinimumSize = new Size(880, 560);
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96, 96);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 10);
        using (var iconStream = typeof(NativeWorkspaceForm).Assembly.GetManifestResourceStream("KhitunGeo.BrandIcon.ico"))
            if (iconStream is not null) Icon = new Icon(iconStream);

        var sidebar = new FlowLayoutPanel { Dock = DockStyle.Left, Width = 210, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(12, 12, 8, 12) };
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
        foreach (var button in new[] { passportButton, installUpdateButton, cancelUpdateButton }) { button.Width = 178; button.Height = 40; NativeTheme.StyleNavigation(button); }
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
        split.Panel1.Controls.Add(BuildGridHost());
        split.Panel2.Controls.Add(BuildPreviewHost());
        var footer = new StatusStrip();
        footer.Items.Add(status);
        footer.Items.Add(cancelPdf);
        cancelPdf.Click += (_, _) => pdfCancellation?.Cancel();
        var content = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Padding = new Padding(12), Margin = Padding.Empty };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 150));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        conversion.Dock = DockStyle.Fill;
        conversion.Margin = new Padding(0, 0, 0, 12);
        content.Controls.Add(conversion, 0, 0);
        split.Margin = Padding.Empty;
        split.SplitterWidth = 12;
        content.Controls.Add(split, 0, 1);
        var body = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty };
        body.Controls.Add(content); body.Controls.Add(sidebar);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = Padding.Empty };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        layout.Controls.Add(BuildApplicationHeader(sidebar), 0, 0);
        layout.Controls.Add(body, 0, 1);
        footer.Dock = DockStyle.Fill; footer.Margin = Padding.Empty;
        layout.Controls.Add(footer, 0, 2);
        Controls.Add(layout);
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
        var button = new Button { Text = text, Width = 178, Height = 36, FlatStyle = FlatStyle.Flat, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(8, 0, 0, 0) };
        NativeTheme.StyleNavigation(button);
        button.Click += (_, _) => action();
        parent.Controls.Add(button);
        return button;
    }

    private Control BuildApplicationHeader(FlowLayoutPanel sidebar)
    {
        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 7, RowCount = 1, Padding = new Padding(14, 10, 14, 10), Margin = Padding.Empty, Tag = "surface" };
        header.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        foreach (var width in new[] { 38, 48, 220 }) header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, width));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        foreach (var width in new[] { 140, 106, 106 }) header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, width));
        var toggle = new Button { Text = "☰", Dock = DockStyle.Fill, FlatStyle = FlatStyle.Flat, AccessibleName = "Показать или скрыть левую панель", Margin = new Padding(0, 4, 4, 4) };
        NativeTheme.StyleNavigation(toggle);
        toggle.Click += (_, _) => sidebar.Visible = !sidebar.Visible;
        var logo = new PictureBox { Name = "ApplicationLogo", Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, Margin = new Padding(4), AccessibleName = "Логотип Khitun Geo" };
        using (var source = typeof(NativeWorkspaceForm).Assembly.GetManifestResourceStream("KhitunGeo.BrandMark.png"))
            if (source is not null) { using var image = Image.FromStream(source); logo.Image = new Bitmap(image); }
        logo.Disposed += (_, _) => logo.Image?.Dispose();
        var identity = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = new Padding(8, 0, 0, 0) };
        identity.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        identity.RowStyles.Add(new RowStyle(SizeType.Percent, 58)); identity.RowStyles.Add(new RowStyle(SizeType.Percent, 42));
        identity.Controls.Add(new Label { Text = "Khitun Geo", Dock = DockStyle.Fill, Font = new Font(Font.FontFamily, 14, FontStyle.Bold), TextAlign = ContentAlignment.BottomLeft, Margin = Padding.Empty }, 0, 0);
        identity.Controls.Add(new Label { Text = "Геодезическая рабочая среда", Dock = DockStyle.Fill, Font = new Font(Font.FontFamily, 8.5f), TextAlign = ContentAlignment.TopLeft, Tag = "muted", Margin = Padding.Empty }, 0, 1);
        var import = HeaderButton("Импорт", true);
        var export = HeaderButton("Экспорт");
        visualizationHeaderButton = HeaderButton("Вид: скрыть");
        import.Click += (_, _) => ImportText(); export.Click += (_, _) => ExportPoints();
        visualizationHeaderButton.Click += (_, _) => ToggleVisualization();
        header.Controls.Add(toggle, 0, 0); header.Controls.Add(logo, 1, 0); header.Controls.Add(identity, 2, 0);
        header.Controls.Add(new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty }, 3, 0);
        header.Controls.Add(visualizationHeaderButton, 4, 0); header.Controls.Add(import, 5, 0); header.Controls.Add(export, 6, 0);
        return header;
    }

    private static Button HeaderButton(string text, bool primary = false)
    {
        var button = new Button { Text = text, Dock = DockStyle.Fill, FlatStyle = FlatStyle.Flat, Margin = new Padding(4), Font = new Font("Segoe UI", 10, FontStyle.Bold) };
        button.Tag = primary ? "primary" : "button";
        return button;
    }

    private Control BuildGridHost()
    {
        var host = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Padding = new Padding(10), Margin = Padding.Empty, Tag = "surface" };
        host.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        host.RowStyles.Add(new RowStyle(SizeType.Absolute, 40)); host.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        host.Controls.Add(new Label { Text = "Таблица точек", Dock = DockStyle.Fill, Font = new Font(Font, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft, Margin = Padding.Empty }, 0, 0);
        grid.Margin = Padding.Empty; host.Controls.Add(grid, 0, 1);
        return host;
    }

    private Control BuildPreviewHost()
    {
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Padding = new Padding(10), Margin = Padding.Empty, Tag = "surface" };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var toolbar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 5, RowCount = 1, Margin = Padding.Empty };
        toolbar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        foreach (var width in new[] { 32, 52, 32, 76 }) toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, width));
        var title = new Label { Text = "Визуализация", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = new Font(Font, FontStyle.Bold), Margin = Padding.Empty, AutoEllipsis = true };
        var minus = HeaderButton("−"); var plus = HeaderButton("+"); var fit = HeaderButton("Вписать");
        minus.Margin = plus.Margin = fit.Margin = new Padding(1, 4, 1, 4);
        var zoom = new Label { Text = "100%", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Font = new Font(Font.FontFamily, 8.5f), Margin = Padding.Empty };
        fit.Click += (_, _) => preview.FitToContent(); plus.Click += (_, _) => preview.ZoomBy(1.2); minus.Click += (_, _) => preview.ZoomBy(1 / 1.2);
        preview.ZoomChanged += (_, _) => zoom.Text = $"{preview.ZoomPercent}%";
        toolbar.Controls.Add(title, 0, 0); toolbar.Controls.Add(minus, 1, 0); toolbar.Controls.Add(zoom, 2, 0); toolbar.Controls.Add(plus, 3, 0); toolbar.Controls.Add(fit, 4, 0);
        layout.Controls.Add(toolbar, 0, 0); preview.Margin = Padding.Empty; layout.Controls.Add(preview, 0, 1);
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
        using var dialog = CreateHeightDialog();
        dialog.ShowDialog(this);
    }

    private Form CreateHeightDialog()
    {
        var dialog = new Form { Text = "Изменить высоты всех точек", ClientSize = new Size(530, 340), Font = new Font("Segoe UI", 10), AutoScaleMode = AutoScaleMode.Dpi, AutoScaleDimensions = new SizeF(96, 96), StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false, Icon = Icon };
        var operation = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        operation.Items.AddRange(new object[] {
            "Абсолютная отметка − Z каждой точки",
            "Абсолютная отметка + Z каждой точки",
            "Z каждой точки + значение",
            "Z каждой точки − значение",
            "Задать одну отметку всем точкам"
        });
        operation.SelectedIndex = 0;
        var valueLabel = new Label { Text = "Абсолютная отметка, м", Dock = DockStyle.Fill, TextAlign = ContentAlignment.BottomLeft };
        var value = CreateHeightInput();
        var formula = new Label { Dock = DockStyle.Fill, Padding = new Padding(10), Tag = "hint", TextAlign = ContentAlignment.MiddleLeft };
        var fields = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(18), ColumnCount = 1, RowCount = 7 };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        foreach (var height in new[] { 26, 40, 28, 38 }) fields.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        fields.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 32)); fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        fields.Controls.Add(new Label { Text = "Операция для всей таблицы", Dock = DockStyle.Fill, Font = new Font(Font, FontStyle.Bold) }, 0, 0);
        fields.Controls.Add(operation, 0, 1); fields.Controls.Add(valueLabel, 0, 2); fields.Controls.Add(value, 0, 3); fields.Controls.Add(formula, 0, 4);
        fields.Controls.Add(new Label { Text = "Пустые Z сохраняются. Отмена изменения — Ctrl+Z.", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Tag = "muted" }, 0, 5);
        var buttons = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Margin = Padding.Empty };
        buttons.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100)); buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        var cancel = HeaderButton("Отмена"); cancel.DialogResult = DialogResult.Cancel;
        var apply = HeaderButton("Применить ко всем", true);
        buttons.Controls.Add(cancel, 1, 0); buttons.Controls.Add(apply, 2, 0); fields.Controls.Add(buttons, 0, 6);
        dialog.Controls.Add(fields); dialog.AcceptButton = apply; dialog.CancelButton = cancel;
        HeightOperation Mode() => operation.SelectedIndex switch {
            0 => HeightOperation.AbsoluteMinusPoint, 1 => HeightOperation.AbsolutePlusPoint,
            2 => HeightOperation.AddToAll, 3 => HeightOperation.SubtractFromAll, 4 => HeightOperation.SetAll,
            _ => throw new InvalidOperationException("Выберите операцию высоты.")
        };
        void UpdateFields()
        {
            var amount = (double)value.Value;
            valueLabel.Text = operation.SelectedIndex is 0 or 1 or 4 ? "Абсолютная отметка, м" : "Значение, м";
            formula.Text = operation.SelectedIndex switch {
                0 => $"Z новое = {amount:0.###} − Z каждой точки",
                1 => $"Z новое = {amount:0.###} + Z каждой точки",
                2 => $"Z новое = Z каждой точки + {amount:0.###}",
                3 => $"Z новое = Z каждой точки − {amount:0.###}",
                _ => $"Z каждой точки = {amount:0.###}"
            };
            var sample = workspace.Points.FirstOrDefault(p => p.Height is not null);
            if (sample is not null) {
                try { formula.Text += $"\nТочка {sample.Name}: {sample.Height:0.###} → {HeightCalculator.Apply(new[] { sample }, Mode(), amount)[0].Height:0.###} м"; }
                catch (ArgumentException) { formula.Text += "\nРезультат за пределами допустимого диапазона."; }
            }
            apply.Enabled = workspace.Points.Count > 0;
        }
        operation.SelectedIndexChanged += (_, _) => UpdateFields(); value.ValueChanged += (_, _) => UpdateFields(); UpdateFields();
        apply.Click += (_, _) => {
            try { workspace.ReplacePoints(HeightCalculator.Apply(workspace.Points, Mode(), (double)value.Value)); dialog.DialogResult = DialogResult.OK; dialog.Close(); }
            catch (ArgumentException ex) { MessageBox.Show(dialog, ex.Message, "Расчёт высоты"); }
        };
        NativeTheme.Apply(dialog, dark);
        return dialog;
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
