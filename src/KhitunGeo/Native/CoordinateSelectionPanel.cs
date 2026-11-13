using System.IO;
using System.Text.Json;

namespace KhitunGeo.Native;

internal sealed class CoordinateSelectionPanel : TableLayoutPanel
{
    private readonly ComboBox source = Picker();
    private readonly ComboBox target = Picker();
    private bool autoZone = true, sourcePrefix = true, targetPrefix = true;
    private int manualZone = 28;
    private readonly Button convert = new() { Text = "Преобразовать", Dock = DockStyle.Fill, Enabled = false, Tag = "primary", FlatStyle = FlatStyle.Flat };
    private readonly Label message = new() { Text = "Расчёт недоступен", Dock = DockStyle.Fill, Margin = Padding.Empty, AutoEllipsis = true, Tag = "muted" };
    private IReadOnlyList<MskCrsEntry> entries = Array.Empty<MskCrsEntry>();
    private NativeCrsOption? sourceCurrent, targetCurrent;
    private Selection? sourceBefore, targetBefore;
    private bool selecting;
    private readonly Button sourceZone = ZoneButton(), targetZone = ZoneButton();
    private bool catalogueReady, runtimeReady, busy, ambiguousSourceZones;
    public event EventHandler? ConvertRequested;
    public event Action<Action>? SettingsChanged;
    public bool UsesAutoTargetZone => autoZone && IsZoned(target);
    public bool SourceIsGeographic => (Kind(source) ?? throw new InvalidOperationException("Выберите исходную систему координат.")) == "geo";
    public string SourceName => sourceCurrent?.Name ?? "";
    public string TargetName => targetCurrent?.Name ?? "";

    public CoordinateSelectionPanel(string cataloguePath, Action swapPointAxes)
    {
        Dock = DockStyle.Top; Height = 112; ColumnCount = 4; RowCount = 4; Padding = new Padding(12, 4, 12, 4);
        Name = "CoordinateCard"; Tag = "surface";
        ColumnStyles.Add(new(SizeType.Percent, 50)); ColumnStyles.Add(new(SizeType.Absolute, 48));
        ColumnStyles.Add(new(SizeType.Percent, 50)); ColumnStyles.Add(new(SizeType.Absolute, 154));
        foreach (var height in new[] { 20, 36, 28 }) RowStyles.Add(new(SizeType.Absolute, height));
        RowStyles.Add(new(SizeType.Percent, 100));
        Controls.Add(Caption("Исходная СК"), 0, 0); Controls.Add(Caption("Целевая СК"), 2, 0);
        source.AccessibleName = "Исходная СК — открыть поиск"; target.AccessibleName = "Целевая СК — открыть поиск";
        Controls.Add(source, 0, 1); Controls.Add(target, 2, 1);
        var swap = new Button { Name = "SwapCrs", Text = "↔", AccessibleName = "Поменять системы координат местами", Dock = DockStyle.Fill, Margin = new Padding(4, 1, 4, 1) };
        swap.Click += (_, _) => SwapSystems(); Controls.Add(swap, 1, 1);
        convert.Margin = new Padding(8, 1, 0, 1);
        convert.Click += (_, _) => ConvertRequested?.Invoke(this, EventArgs.Empty);
        Controls.Add(convert, 3, 1);
        var axes = new Button { Name = "SwapAxes", Text = "Поменять X и Y", AccessibleName = "Поменять X и Y у всех точек", Dock = DockStyle.Fill, Tag = "zone", Margin = new Padding(8, 0, 0, 0) };
        axes.Click += (_, _) => swapPointAxes(); Controls.Add(axes, 3, 2);
        Controls.Add(sourceZone, 0, 2); Controls.Add(targetZone, 2, 2);
        sourceZone.Click += (_, _) => ChooseZone(source); targetZone.Click += (_, _) => ChooseZone(target);
        Controls.Add(message, 0, 3); SetColumnSpan(message, 4);
        foreach (var picker in new[] { source, target })
        {
            picker.SelectedIndexChanged += (_, _) => SelectionChanged(picker);
            picker.SelectionChangeCommitted += (_, _) => ChooseZone(picker);
        }
        try
        {
            entries = MskSelectionCatalogue.Build(NativeCrsCatalogue.Read(cataloguePath));
            source.DataSource = entries.ToList(); target.DataSource = entries.ToList();
            Select(source, entries.Single(e => e.Id == "wgs").Zones[0]);
            Select(target, entries.Single(e => e.Id == "msk-group:164").Zones[0]);
            catalogueReady = true;
            UpdateControls();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or FormatException or KeyNotFoundException or InvalidOperationException)
        {
            source.Enabled = target.Enabled = swap.Enabled = sourceZone.Enabled = targetZone.Enabled = false;
            message.Text = "Каталог систем координат недоступен: " + ex.Message;
        }
    }

    private static Label Caption(string text) => new() { Text = text, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Margin = Padding.Empty };
    private static Button ZoneButton() => new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true, Margin = Padding.Empty, Tag = "zone" };
    private static ComboBox Picker() => new SearchableCrsPicker()
    {
        BindingContext = new BindingContext(),
        Dock = DockStyle.Fill, Margin = new Padding(0, 2, 0, 2), DropDownStyle = ComboBoxStyle.DropDownList, DropDownWidth = 620,
        DisplayMember = nameof(MskCrsEntry.Name), ValueMember = nameof(MskCrsEntry.Id),
        AutoCompleteSource = AutoCompleteSource.ListItems, AutoCompleteMode = AutoCompleteMode.SuggestAppend
    };

    private NativeCrsOption? Current(ComboBox picker) => picker == source ? sourceCurrent : targetCurrent;
    private string? Kind(ComboBox picker) => Current(picker)?.Definition.GetProperty("kind").GetString();
    private bool IsZoned(ComboBox picker) => Kind(picker) is "gk6" or "gk3" or "utm";
    private int MaximumZone => Kind(source) is "gk6" or "utm" || Kind(target) is "gk6" or "utm" ? 60 : 120;

    public bool SelectSource(string id)
    {
        var option = entries.SelectMany(e => e.Zones).FirstOrDefault(item => string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase));
        if (option is null) return false;
        Select(source, option);
        ambiguousSourceZones = false;
        UpdateControls();
        return true;
    }

    private void Select(ComboBox picker, NativeCrsOption option)
    {
        selecting = true;
        try
        {
            if (picker == source) { sourceCurrent = option; sourceBefore = null; }
            else { targetCurrent = option; targetBefore = null; }
            picker.SelectedItem = entries.First(e => e.Zones.Contains(option));
        }
        finally { selecting = false; }
    }

    private void SelectionChanged(ComboBox picker)
    {
        if (selecting || !catalogueReady || picker.SelectedItem is not MskCrsEntry entry) return;
        var before = CaptureSelection();
        if (picker == source)
        {
            sourceBefore = before; sourceCurrent = entry.Zones[0]; ambiguousSourceZones = false;
        }
        else { targetBefore = before; targetCurrent = entry.Zones[0]; }
        UpdateControls();
    }

    private void ChooseZone(ComboBox picker)
    {
        if (picker.SelectedItem is not MskCrsEntry entry) return;
        var isSource = picker == source;
        var before = (isSource ? sourceBefore : targetBefore) ?? CaptureSelection();
        sourceBefore = targetBefore = null;

        if (IsZoned(picker))
        {
            var kind = Kind(picker)!;
            using var dialog = new ZonedCrsDialog(Current(picker)!.Name, kind, manualZone, autoZone,
                isSource ? sourcePrefix : targetPrefix, isSource, NativeTheme.IsDark(this), MaximumZone,
                Kind(source) is "gk3" or "gk6" && !sourcePrefix);
            if (UiMotion.ShowDialog(dialog, FindForm()) != DialogResult.OK) { Restore(before); return; }
            autoZone = dialog.Automatic;
            manualZone = dialog.SelectedZone;
            if (kind != "utm")
            {
                if (isSource) sourcePrefix = dialog.HasPrefix; else targetPrefix = dialog.HasPrefix;
            }
        }
        else if (entry.IsZoneGroup && entry.Zones.Count > 1)
        {
            using var dialog = new MskZoneDialog(entry, Current(picker)!, NativeTheme.IsDark(this));
            if (UiMotion.ShowDialog(dialog, FindForm()) != DialogResult.OK || dialog.SelectedZone is not NativeCrsOption selected)
            { Restore(before); return; }
            Select(picker, selected);
        }

        if (isSource) ambiguousSourceZones = false;
        UpdateControls();
        RecordChange(before);
    }

    private void SwapSystems()
    {
        if (sourceCurrent is null || targetCurrent is null) return;
        var before = CaptureSelection();
        Select(source, before.Target); Select(target, before.Source);
        (sourcePrefix, targetPrefix) = (targetPrefix, sourcePrefix);
        ambiguousSourceZones = false;
        UpdateControls(); RecordChange(before);
    }

    private void UpdateControls()
    {
        manualZone = Math.Clamp(manualZone, 1, MaximumZone);
        if (Kind(source) is "gk6" or "gk3" && !sourcePrefix) autoZone = false;
        foreach (var (picker, button) in new[] { (source, sourceZone), (target, targetZone) })
        {
            button.Visible = IsZoned(picker) || picker.SelectedItem is MskCrsEntry { IsZoneGroup: true, Zones.Count: > 1 };
            if (!IsZoned(picker)) button.Text = Current(picker)?.Name + " ▾";
            else
            {
                var automatic = autoZone && (picker == target || Kind(source) != "utm");
                button.Text = (automatic ? picker == source ? "Зона из префикса Y" : "Зона по долготе" : $"Зона {manualZone}") + " ▾";
            }
        }
        UpdateConvertButton();
    }

    private void UpdateConvertButton() => convert.Enabled = busy || (catalogueReady && runtimeReady && !ambiguousSourceZones);

    public void SetRuntimeReady(bool ready, string? error = null)
    {
        runtimeReady = ready;
        if (catalogueReady) message.Text = ready ? "" : "Расчёт недоступен: " + error;
        UpdateConvertButton();
    }

    public void SetBusy(bool value)
    {
        busy = value; convert.Text = value ? "Прервать" : "Преобразовать"; UpdateConvertButton();
    }

    private sealed record Selection(NativeCrsOption Source, NativeCrsOption Target, bool Auto, int Zone, bool SourcePrefix, bool TargetPrefix, bool Ambiguous, string Message);
    private Selection CaptureSelection() => new(sourceCurrent!, targetCurrent!, autoZone, manualZone, sourcePrefix, targetPrefix, ambiguousSourceZones, message.Text);
    private void Restore(Selection state)
    {
        Select(source, state.Source); Select(target, state.Target);
        autoZone = state.Auto; manualZone = state.Zone;
        sourcePrefix = state.SourcePrefix; targetPrefix = state.TargetPrefix;
        ambiguousSourceZones = state.Ambiguous; message.Text = state.Message;
        UpdateControls();
    }
    private void RecordChange(Selection before)
    {
        if (CaptureSelection() != before) SettingsChanged?.Invoke(() => Restore(before));
    }
    public Action CaptureUndo()
    {
        var before = CaptureSelection();
        return () => Restore(before);
    }

    public void AcceptResult(IReadOnlyList<int> zones)
    {
        Select(source, targetCurrent!);
        sourcePrefix = targetPrefix;
        var explicitZone = Kind(source) == "utm" || ((Kind(source) is "gk6" or "gk3") && !sourcePrefix);
        if (explicitZone && zones.Count == 1)
        {
            manualZone = zones[0];
            if (Kind(source) != "utm") autoZone = false;
        }
        ambiguousSourceZones = explicitZone && zones.Count > 1;
        message.Text = ambiguousSourceZones
            ? "Разные зоны без префикса: для нового расчёта выберите исходную СК."
            : "Таблица в СК: " + SourceName;
        UpdateControls();
    }

    public string SettingsSnapshot() => BuildRequest(Array.Empty<SurveyPoint>());

    public string BuildRequest(IReadOnlyList<SurveyPoint> points)
    {
        if (sourceCurrent is null || targetCurrent is null)
            throw new InvalidOperationException("Выберите исходную и целевую СК.");
        return CoordinateBridge.BuildRequest(points, sourceCurrent.Definition, targetCurrent.Definition,
            new(autoZone, manualZone, (Kind(source) is "gk6" or "gk3") && sourcePrefix,
                (Kind(target) is "gk6" or "gk3") && targetPrefix));
    }
}
