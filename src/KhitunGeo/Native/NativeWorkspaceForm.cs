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

    public NativeWorkspaceForm()
    {
        Text = "Khitun Geo";
        Width = 1280;
        Height = 800;
        MinimumSize = new Size(800, 500);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 10);

        var sidebar = new FlowLayoutPanel { Dock = DockStyle.Left, Width = 150, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(8) };
        sidebar.Controls.Add(new Label { Text = "Khitun Geo", AutoSize = true, Margin = new Padding(5, 12, 5, 20) });
        AddButton(sidebar, "Добавить точку", () =>
        {
            if (grid.EndEdit()) workspace.ReplacePoints(workspace.Points.Append(new SurveyPoint((workspace.Points.Count + 1).ToString(), null, null, null, "")).ToArray());
        });
        AddButton(sidebar, "Импорт данных", ImportText);
        AddButton(sidebar, "Импорт DWG/DXF", async () => await ImportDrawing());
        AddButton(sidebar, "Убрать чертёж", ClearDrawing);
        AddButton(sidebar, "Слои", ShowDrawingLayers);
        AddButton(sidebar, "Экспорт точек", ExportPoints);
        AddButton(sidebar, "Сохранить CSV", () => SaveCsv());
        sidebar.Controls.Add(passportButton);
        passportButton.Click += async (_, _) => await ExportPassportPdf();
        AddButton(sidebar, "Высота ±", OffsetHeight);
        AddButton(sidebar, "Отменить", () => { if (grid.EndEdit()) workspace.Undo(); });
        AddButton(sidebar, "Визуализация", () =>
        {
            var enabled = split.Panel2Collapsed;
            preview.SetActive(enabled);
            split.Panel2Collapsed = !enabled;
        });
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
        split.Panel2.Controls.Add(preview);
        var footer = new StatusStrip();
        footer.Items.Add(status);
        footer.Items.Add(cancelPdf);
        cancelPdf.Click += (_, _) => pdfCancellation?.Cancel();
        var content = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        content.Controls.Add(split);
        content.Controls.Add(conversion);
        content.Controls.Add(footer);
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
        var button = new Button { Text = text, Width = 130, Height = 40, FlatStyle = FlatStyle.Flat };
        button.Click += (_, _) => action();
        parent.Controls.Add(button);
        return button;
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
            Filter = "Все поддерживаемые данные|*.xlsx;*.csv;*.tsv;*.txt|Книга Excel (*.xlsx)|*.xlsx|CSV (*.csv)|*.csv|TSV (*.tsv)|*.tsv|Текст (*.txt)|*.txt|Все файлы (*.*)|*.*",
            FilterIndex = 1, Multiselect = true, CheckFileExists = true
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        ImportFiles(dialog.FileNames);
    }

    private static bool IsSupportedDataFile(string path) => Path.GetExtension(path).ToLowerInvariant() is ".xlsx" or ".csv" or ".tsv" or ".txt";

    private void ImportFiles(string[] paths)
    {
        if (!grid.EndEdit()) return;
        try
        {
            var imported = paths.SelectMany(PointFileService.Read).ToArray();
            workspace.ReplacePoints(workspace.Points.Concat(imported).ToArray());
        }
        catch (Exception ex) when (ex is FormatException or IOException or UnauthorizedAccessException or System.Text.DecoderFallbackException or System.Xml.XmlException)
        { MessageBox.Show(this, ex.Message, "Импорт данных"); }
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
        using var dialog = new Form { Text = "Расчёт высоты всех точек", Width = 430, Height = 270, StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false };
        var operation = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        operation.Items.AddRange(new object[]
        {
            "Задать высоту всем точкам",
            "Прибавить к высоте каждой точки",
            "Вычесть из высоты каждой точки",
            "Абсолютная отметка − глубина"
        });
        operation.SelectedIndex = 0;
        var firstLabel = new Label { Text = "Высота / значение", AutoSize = true, Anchor = AnchorStyles.Left };
        var firstValue = CreateHeightInput();
        var secondLabel = new Label { Text = "Глубина", AutoSize = true, Anchor = AnchorStyles.Left };
        var secondValue = CreateHeightInput();
        var preview = new Label { AutoSize = true, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
        var fields = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 2, RowCount = 5 };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 56));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44));
        fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        fields.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        fields.Controls.Add(new Label { Text = "Операция", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        fields.Controls.Add(operation, 1, 0);
        fields.Controls.Add(firstLabel, 0, 1);
        fields.Controls.Add(firstValue, 1, 1);
        fields.Controls.Add(secondLabel, 0, 2);
        fields.Controls.Add(secondValue, 1, 2);
        fields.Controls.Add(preview, 0, 4);
        fields.SetColumnSpan(preview, 2);
        var apply = new Button { Text = "Применить ко всем точкам", Dock = DockStyle.Bottom, Height = 38, DialogResult = DialogResult.None };
        dialog.Controls.Add(fields);
        dialog.Controls.Add(apply);
        dialog.AcceptButton = apply;

        void UpdateFields()
        {
            var isDifference = operation.SelectedIndex == 3;
            firstLabel.Text = isDifference ? "Абсолютная отметка" : "Высота / значение";
            secondLabel.Visible = secondValue.Visible = isDifference;
            preview.Text = isDifference
                ? $"Результат для каждой точки: {(double)firstValue.Value - (double)secondValue.Value:0.###}"
                : operation.SelectedIndex == 0
                    ? $"Высота будет задана всем точкам: {(double)firstValue.Value:0.###}"
                    : "Пустые значения Z останутся пустыми. Действие можно отменить через Ctrl+Z.";
        }

        operation.SelectedIndexChanged += (_, _) => UpdateFields();
        firstValue.ValueChanged += (_, _) => UpdateFields();
        secondValue.ValueChanged += (_, _) => UpdateFields();
        UpdateFields();
        apply.Click += (_, _) =>
        {
            try
            {
                SurveyPoint[] adjusted;
                if (operation.SelectedIndex == 3)
                {
                    if (secondValue.Value < 0) throw new ArgumentOutOfRangeException("depth", "Глубина должна быть неотрицательной.");
                    var result = HeightCalculator.AbsoluteMinusDepth((double)firstValue.Value, (double)secondValue.Value);
                    adjusted = HeightCalculator.Apply(workspace.Points, HeightOperation.SetAll, result);
                }
                else
                {
                    var mode = operation.SelectedIndex switch
                    {
                        0 => HeightOperation.SetAll,
                        1 => HeightOperation.AddToAll,
                        2 => HeightOperation.SubtractFromAll,
                        _ => throw new InvalidOperationException("Выберите операцию высоты.")
                    };
                    adjusted = HeightCalculator.Apply(workspace.Points, mode, (double)firstValue.Value);
                }
                workspace.ReplacePoints(adjusted);
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
