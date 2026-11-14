namespace KhitunGeo.Native;

internal sealed class TrainingDialog : Form
{
    private readonly EmbeddedCoordinateRuntime? runtime;
    private readonly AppSettings settings;
    private readonly bool dark;
    private readonly PointWorkspace lesson = new();
    private readonly ComboBox source = Picker(), target = Picker();
    private readonly NumericUpDown zone = new() { Minimum = 1, Maximum = 60, Value = 7, Width = 70 };
    private readonly Label zoneLabel = new() { Text = "Исходная зона", AutoSize = true, Anchor = AnchorStyles.Left };
    private readonly Label instruction = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
    private readonly DataGridView grid = new() { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, RowHeadersVisible = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill };
    private readonly TextBox guide = new() { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Visible = false };
    private readonly Button next = new() { AutoSize = true, Tag = "primary" };
    private readonly Button restart = new() { Text = "Начать заново", AutoSize = true };
    private NativeCrsOption? wgs;
    private enum Stage { Import, Convert, Export, Finished }
    private Stage stage;

    public TrainingDialog(EmbeddedCoordinateRuntime? runtime, AppSettings settings, bool dark)
    {
        this.runtime = runtime;
        this.settings = settings;
        this.dark = dark;
        Text = "Обучение — преобразование и экспорт точек";
        Font = new Font("Segoe UI", 10);
        AutoScaleMode = AutoScaleMode.Dpi;
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(680, 430);
        ClientSize = new Size(870, 540);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = new Padding(16) };
        layout.RowStyles.Add(new(SizeType.Absolute, 84));
        layout.RowStyles.Add(new(SizeType.AutoSize));
        layout.RowStyles.Add(new(SizeType.AutoSize));
        layout.RowStyles.Add(new(SizeType.Percent, 100));
        layout.RowStyles.Add(new(SizeType.AutoSize));
        layout.Controls.Add(instruction, 0, 0);
        var pickers = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2 };
        pickers.ColumnStyles.Add(new(SizeType.Percent, 50));
        pickers.ColumnStyles.Add(new(SizeType.Percent, 50));
        pickers.Controls.Add(new Label { Text = "Исходная система координат", AutoSize = true }, 0, 0);
        pickers.Controls.Add(new Label { Text = "Целевая система координат", AutoSize = true }, 1, 0);
        pickers.Controls.Add(source, 0, 1);
        pickers.Controls.Add(target, 1, 1);
        layout.Controls.Add(pickers, 0, 1);
        var zones = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top };
        zones.Controls.Add(zoneLabel); zones.Controls.Add(zone);
        layout.Controls.Add(zones, 0, 2);
        var body = new Panel { Dock = DockStyle.Fill };
        body.Controls.Add(grid); body.Controls.Add(guide);
        layout.Controls.Add(body, 0, 3);
        var actions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Bottom, Padding = new Padding(0, 8, 0, 0) };
        var skip = new Button { Text = "Закрыть обучение", AutoSize = true, DialogResult = DialogResult.Cancel };
        actions.Controls.AddRange(new Control[] { next, restart, skip });
        layout.Controls.Add(actions, 0, 4);
        Controls.Add(layout);
        AcceptButton = next; CancelButton = skip;
        source.SelectedIndexChanged += (_, _) => ConfigureZone();
        next.Click += (_, _) => Advance();
        restart.Click += (_, _) => ResetLesson();
        try
        {
            var entries = MskSelectionCatalogue.Build(NativeCrsCatalogue.Read(
                Path.Combine(AppContext.BaseDirectory, "wwwroot", "crs", "native-systems.json")));
            var systems = entries.SelectMany(entry => entry.Zones).ToArray();
            wgs = systems.First(s => s.Id == "wgs");
            source.DataSource = systems.ToList(); target.DataSource = systems.ToList();
            source.SelectedItem = wgs; target.SelectedItem = entries.Single(e => e.Id == "msk-group:164").Zones[0];
            ResetLesson();
        }
        catch (Exception ex)
        {
            instruction.Text = "Не удалось открыть каталог систем координат. " + ex.Message;
            next.Enabled = restart.Enabled = false;
        }
        NativeTheme.Apply(this, dark);
    }

    private static ComboBox Picker() => new()
    {
        Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, DropDownWidth = 560,
        DisplayMember = nameof(NativeCrsOption.Name), BindingContext = new BindingContext(),
        AutoCompleteSource = AutoCompleteSource.ListItems, AutoCompleteMode = AutoCompleteMode.SuggestAppend
    };

    private static string Kind(NativeCrsOption option) => option.Definition.GetProperty("kind").GetString()!;

    private void ConfigureZone()
    {
        var kind = source.SelectedItem is NativeCrsOption option ? Kind(option) : "";
        zoneLabel.Visible = zone.Visible = kind is "gk3" or "gk6" or "utm";
        zone.Maximum = kind == "gk3" ? 120 : 60;
        zone.Value = kind switch { "utm" => 37, "gk3" => 13, _ => 7 };
    }

    private void ResetLesson()
    {
        lesson.ReplacePoints(Array.Empty<SurveyPoint>());
        grid.DataSource = null; grid.Visible = true; guide.Visible = false;
        source.Enabled = target.Enabled = zone.Enabled = true;
        stage = Stage.Import;
        instruction.Text = runtime is null ? "Модуль расчёта недоступен. Проверьте установку приложения." :
            "Шаг 1 из 3. Выберите исходную и целевую СК. Откройте учебный CSV и проверьте в мастере столбцы X, Y и высоты. Учебные точки загружаются в отдельную таблицу.";
        next.Enabled = runtime is not null;
        next.Text = "Импортировать пример…";
    }

    private void Advance()
    {
        if (runtime is null || wgs is null || source.SelectedItem is not NativeCrsOption from || target.SelectedItem is not NativeCrsOption to) return;
        try
        {
            switch (stage)
            {
                case Stage.Import:
                    if (!ImportExample(from, to)) return;
                    source.Enabled = target.Enabled = zone.Enabled = false;
                    stage = Stage.Convert;
                    ShowPoints(from);
                    instruction.Text = $"Шаг 2 из 3. Загружено точек: {lesson.Points.Count}. Проверьте исходные координаты. Нажмите «Преобразовать», чтобы получить координаты в СК «{to.Name}».";
                    next.Text = "Преобразовать";
                    break;
                case Stage.Convert:
                    var sourceKind = Kind(from);
                    var zones = Convert(lesson, from, to, new(true, (double)zone.Value, sourceKind is "gk3" or "gk6", Kind(to) is "gk3" or "gk6"));
                    ShowPoints(to);
                    stage = Stage.Export;
                    instruction.Text = $"Шаг 3 из 3. Таблица в СК «{to.Name}»." + (zones.Count > 0 ? $" Целевая зона: {string.Join(", ", zones)}." : "") +
                        " Откройте экспорт, выберите формат, проверьте порядок координат и сохраните файл.";
                    next.Text = "Экспортировать…";
                    break;
                case Stage.Export:
                    using (var dialog = new PointExportDialog(lesson.Points, Kind(to) == "geo", to.Name, dark, settings))
                    {
                        if (dialog.ShowDialog(this) != DialogResult.OK || dialog.SavedOptions is not { } saved) return;
                        instruction.Text = $"Обучение завершено. Сохранено точек: {dialog.ExportedCount}. Система координат: {to.Name}.";
                        guide.Text = TrainingExportGuide.Describe(saved);
                    }
                    grid.Visible = false; guide.Visible = true; guide.Select(0, 0);
                    stage = Stage.Finished;
                    next.Text = "Готово";
                    break;
                case Stage.Finished:
                    Close();
                    break;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Учебный пример", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private IReadOnlyList<int> Convert(PointWorkspace table, NativeCrsOption from, NativeCrsOption to, CoordinateZoneOptions options)
    {
        var points = table.Points;
        var request = CoordinateBridge.BuildRequest(points, from.Definition, to.Definition, options);
        return CoordinateBridge.ApplyResponse(table, points, runtime!.Convert(request));
    }

    private bool ImportExample(NativeCrsOption from, NativeCrsOption to)
    {
        var kind = Kind(from);
        var zoneNumber = (int)zone.Value;
        var longitude = kind switch
        {
            "fixedtm" => from.Definition.GetProperty("lon0").GetDouble(),
            "utm" => zoneNumber * 6 - 183,
            "gk6" => zoneNumber * 6 - 3,
            "gk3" => zoneNumber * 3,
            _ => Kind(to) == "fixedtm" ? to.Definition.GetProperty("lon0").GetDouble() : 37.62
        };
        // Keep all examples clear of the antimeridian and in the northern hemisphere.
        longitude = (longitude + 540) % 360 - 180;
        var direction = kind == "gk3" && zoneNumber == 120 ? -1 : 1;
        var example = new PointWorkspace();
        example.ReplacePoints(new[]
        {
            new SurveyPoint("101", 55.70, longitude + direction * .001, 134.2, "Репер"),
            new SurveyPoint("102", 55.701, longitude + direction * .002, 135.8, "Угол здания"),
            new SurveyPoint("103", 55.703, longitude + direction * .003, 134.9, "Ось дороги")
        });
        Convert(example, wgs!, from, new(false, zoneNumber, false, kind is "gk3" or "gk6"));
        var path = Path.Combine(Path.GetTempPath(), $"KhitunGeo-учебные-точки-{Guid.NewGuid():N}.csv");
        try
        {
            var rows = example.Points.Select(p => FormattableString.Invariant($"{p.Name};{p.X:R};{p.Y:R};{p.Height:R};{p.Description}"));
            File.WriteAllLines(path, new[] { "Номер;X;Y;Высота;Описание" }.Concat(rows));
            using var wizard = new PointImportWizardForm(path, 0);
            NativeTheme.Apply(wizard, dark);
            if (wizard.ShowDialog(this) != DialogResult.OK) return false;
            lesson.ReplacePoints(wizard.ImportedPoints);
            return true;
        }
        finally { File.Delete(path); }
    }

    private void ShowPoints(NativeCrsOption system)
    {
        var geographic = Kind(system) == "geo";
        grid.DataSource = lesson.Points.Select(p => new { Номер = p.Name, X = p.X, Y = p.Y, Высота = p.Height, Описание = p.Description }).ToList();
        grid.Columns["X"]!.HeaderText = geographic ? "B · широта (°)" : "X · север (м)";
        grid.Columns["Y"]!.HeaderText = geographic ? "L · долгота (°)" : "Y · восток (м)";
        foreach (var axis in new[] { "X", "Y" })
            grid.Columns[axis]!.DefaultCellStyle.Format = geographic ? "F8" : "F3";
    }
}
