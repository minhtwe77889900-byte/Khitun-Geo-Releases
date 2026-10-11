using System.IO;
using System.Text.Json;

namespace KhitunGeo.Native;

internal sealed class CoordinateSelectionPanel : TableLayoutPanel
{
    private readonly ComboBox source = Picker();
    private readonly ComboBox target = Picker();
    private readonly CheckBox autoZone = new() { Text = "Автозона", Checked = true, AutoSize = true };
    private readonly CheckBox sourcePrefix = new() { Text = "Префикс исходного Y", Checked = true, AutoSize = true };
    private readonly CheckBox targetPrefix = new() { Text = "Префикс целевого Y", Checked = true, AutoSize = true };
    private readonly NumericUpDown manualZone = new() { Minimum = 1, Maximum = 60, Value = 28, Width = 58 };

    private readonly Button convert = new() { Text = "Преобразовать", Dock = DockStyle.Fill, Enabled = false, Tag = "primary", FlatStyle = FlatStyle.Flat };
    private readonly Label message = new() { Text = "Расчёт недоступен", AutoSize = false, AutoEllipsis = true, Dock = DockStyle.Fill, Margin = Padding.Empty, Tag = "muted" };
    private IReadOnlyList<MskCrsEntry> entries = Array.Empty<MskCrsEntry>();
    private NativeCrsOption? sourceCurrent, targetCurrent;
    private Action? sourceSelectionUndo, targetSelectionUndo;
    private bool selecting;
    private readonly Button sourceZone = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true, Margin = Padding.Empty };
    private readonly Button targetZone = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true, Margin = Padding.Empty };
    private bool catalogueReady, runtimeReady, busy, ambiguousSourceZones;
    public event EventHandler? ConvertRequested;
    public bool UsesAutoTargetZone => autoZone.Checked && Kind(target) is "gk6" or "gk3" or "utm";
    public bool SourceIsGeographic => (Kind(source) ?? throw new InvalidOperationException("Выберите исходную систему координат.")) == "geo";
    public string SourceName => Current(source)?.Name ?? "";
    public string TargetName => Current(target)?.Name ?? "";

    public bool SelectSource(string id)
    {
        var option = entries.SelectMany(e => e.Zones).FirstOrDefault(item => string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase));
        if (option is null) return false;
        Select(source, option);
        ambiguousSourceZones = false; UpdateConvertButton();
        return true;
    }

    private NativeCrsOption? Current(ComboBox picker)
    {
        if (picker.SelectedItem is not MskCrsEntry entry) return null;
        var current = picker == source ? sourceCurrent : targetCurrent;
        return current is not null && entry.Zones.Any(z => z.Id == current.Id) ? current : entry.Zones[0];
    }

    private void Select(ComboBox picker, NativeCrsOption? option)
    {
        if (option is null) return;
        var wasSelecting = selecting; selecting = true;
        try
        {
            if (picker == source) sourceCurrent = option; else targetCurrent = option;
            picker.SelectedItem = entries.First(e => e.Zones.Any(z => z.Id == option.Id));
            UpdateZoneControls();
        }
        finally { selecting = wasSelecting; }
    }

    private void ChooseZone(ComboBox picker)
    {
        if (picker.SelectedItem is not MskCrsEntry { IsMskGroup: true } entry) return;
        var undo = (picker == source ? sourceSelectionUndo : targetSelectionUndo)
            ?? CaptureSelectionUndo(sourceCurrent, targetCurrent);
        if (picker == source) sourceSelectionUndo = null; else targetSelectionUndo = null;
        using var dialog = new MskZoneDialog(entry, Current(picker)!, NativeTheme.IsDark(this));
        if (dialog.ShowDialog(FindForm()) == DialogResult.OK && dialog.SelectedZone is NativeCrsOption selected)
        {
            if (picker == source) sourceCurrent = selected; else targetCurrent = selected;
            if (picker == source) ambiguousSourceZones = false;
            UpdateZoneControls(); UpdateConvertButton();
        }
        else undo();
    }

    public CoordinateSelectionPanel(string cataloguePath, Action swapPointAxes)
    {
        Dock = DockStyle.Top; Height = 180; ColumnCount = 4; RowCount = 5; Padding = new Padding(14, 8, 14, 8);
        Tag = "surface";
        ColumnStyles.Add(new(SizeType.Percent, 50)); ColumnStyles.Add(new(SizeType.Absolute, 48));
        ColumnStyles.Add(new(SizeType.Percent, 50)); ColumnStyles.Add(new(SizeType.Absolute, 150));
        RowStyles.Add(new(SizeType.Absolute, 24)); RowStyles.Add(new(SizeType.Absolute, 34));
        RowStyles.Add(new(SizeType.Absolute, 26)); RowStyles.Add(new(SizeType.Absolute, 42)); RowStyles.Add(new(SizeType.Absolute, 24));
        Controls.Add(new Label { Text = "Исходная система координат", AutoSize = true, Margin = Padding.Empty, Tag = "muted" }, 0, 0);
        Controls.Add(new Label { Text = "Целевая система координат", AutoSize = true, Margin = Padding.Empty, Tag = "muted" }, 2, 0);
        Controls.Add(source, 0, 1); Controls.Add(target, 2, 1);
        var swap = new Button { Text = "X↔Y", Dock = DockStyle.Fill };
        swap.Click += (_, _) => swapPointAxes(); Controls.Add(swap, 1, 1);
        convert.Click += (_, _) => ConvertRequested?.Invoke(this, EventArgs.Empty);
        Controls.Add(convert, 3, 1);
        var options = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true, Margin = Padding.Empty, Padding = new Padding(0, 5, 0, 0) };
        options.Controls.Add(autoZone); options.Controls.Add(sourcePrefix); options.Controls.Add(targetPrefix);
        options.Controls.Add(new Label { Text = "Зона", AutoSize = true, Margin = new Padding(8, 5, 3, 0) });
        options.Controls.Add(manualZone); Controls.Add(sourceZone, 0, 2); Controls.Add(targetZone, 2, 2);
        sourceZone.Click += (_, _) => ChooseZone(source); targetZone.Click += (_, _) => ChooseZone(target);
        Controls.Add(options, 0, 3); SetColumnSpan(options, 4);
        Controls.Add(message, 0, 4); SetColumnSpan(message, 4);
        source.SelectedIndexChanged += (_, _) =>
        {
            if (!selecting && catalogueReady)
                sourceSelectionUndo = source.SelectedItem is MskCrsEntry { IsMskGroup: true }
                    ? CaptureSelectionUndo(sourceCurrent, Current(target)) : null;
            if (source.SelectedItem is MskCrsEntry { IsMskGroup: false } entry)
            { sourceCurrent = entry.Zones[0]; ambiguousSourceZones = false; }
            UpdateZoneControls(); UpdateConvertButton();
        };
        target.SelectedIndexChanged += (_, _) =>
        {
            if (!selecting && catalogueReady)
                targetSelectionUndo = target.SelectedItem is MskCrsEntry { IsMskGroup: true }
                    ? CaptureSelectionUndo(Current(source), targetCurrent) : null;
            if (target.SelectedItem is MskCrsEntry { IsMskGroup: false } entry) targetCurrent = entry.Zones[0];
            UpdateZoneControls();
        };
        source.SelectionChangeCommitted += (_, _) => ChooseZone(source);
        target.SelectionChangeCommitted += (_, _) => ChooseZone(target);
        autoZone.CheckedChanged += (_, _) => UpdateZoneControls();
        try
        {
            var systems = NativeCrsCatalogue.Read(cataloguePath);
            entries = MskSelectionCatalogue.Build(systems);
            source.DataSource = entries.ToList(); target.DataSource = entries.ToList();
            Select(source, systems.First(c => c.Id == "wgs"));
            Select(target, systems.First(c => c.Id == "msk164"));
            UpdateZoneControls();
            catalogueReady = true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or FormatException or KeyNotFoundException or InvalidOperationException)
        {
            source.Enabled = target.Enabled = options.Enabled = false;
            message.Text = "Каталог систем координат недоступен: " + ex.Message;
        }
    }

    private static ComboBox Picker() => new()
    {
        BindingContext = new BindingContext(),
        Dock = DockStyle.Fill, Margin = new Padding(0, 2, 0, 2), DropDownStyle = ComboBoxStyle.DropDownList, DropDownWidth = 620,
        DisplayMember = nameof(NativeCrsOption.Name), ValueMember = nameof(NativeCrsOption.Id),
        AutoCompleteSource = AutoCompleteSource.ListItems, AutoCompleteMode = AutoCompleteMode.SuggestAppend
    };

    private string? Kind(ComboBox picker) => Current(picker)?.Definition.GetProperty("kind").GetString();

    private void UpdateZoneControls()
    {
        sourceZone.Visible = source.SelectedItem is MskCrsEntry { IsMskGroup: true };
        targetZone.Visible = target.SelectedItem is MskCrsEntry { IsMskGroup: true };
        sourceZone.Text = Current(source)?.Name + " ▾"; targetZone.Text = Current(target)?.Name + " ▾";
        var kinds = new[] { Kind(source), Kind(target) };
        var anyZoned = kinds.Any(k => k is "gk6" or "gk3" or "utm");
        manualZone.Maximum = kinds.Any(k => k is "gk6" or "utm") ? 60 : 120;
        autoZone.Enabled = anyZoned;
        manualZone.Enabled = anyZoned && (!autoZone.Checked || Kind(source) == "utm");
        sourcePrefix.Enabled = Kind(source) is "gk6" or "gk3";
        targetPrefix.Enabled = Kind(target) is "gk6" or "gk3";
    }

    private void UpdateConvertButton() => convert.Enabled = busy || (catalogueReady && runtimeReady && !ambiguousSourceZones);

    public void SetRuntimeReady(bool ready, string? error = null)
    {
        runtimeReady = ready;
        if (catalogueReady) message.Text = ready ? "X — север / широта; Y — восток / долгота" : "Расчёт недоступен: " + error;
        UpdateConvertButton();
    }

    public void SetBusy(bool value)
    {
        busy = value;
        convert.Text = value ? "Прервать" : "Преобразовать";
        UpdateConvertButton();
    }

    public Action CaptureUndo() => CaptureSelectionUndo(Current(source), Current(target));

    private Action CaptureSelectionUndo(NativeCrsOption? s, NativeCrsOption? t)
    {
        var auto = autoZone.Checked; var zone = manualZone.Value;
        var sp = sourcePrefix.Checked; var tp = targetPrefix.Checked;
        var ambiguous = ambiguousSourceZones; var text = message.Text;
        return () =>
        {
            Select(source, s); Select(target, t);
            autoZone.Checked = auto; manualZone.Value = zone;
            sourcePrefix.Checked = sp; targetPrefix.Checked = tp;
            ambiguousSourceZones = ambiguous; message.Text = text;
            UpdateZoneControls(); UpdateConvertButton();
        };
    }

    // Converted coordinates are now in the target CRS; avoid a second conversion
    // interpreting the result as the former source system.
    public void AcceptResult(IReadOnlyList<int> zones)
    {
        Select(source, Current(target));
        sourcePrefix.Checked = targetPrefix.Checked;
        var explicitZone = Kind(source) == "utm" || ((Kind(source) is "gk6" or "gk3") && !sourcePrefix.Checked);
        if (explicitZone && zones.Count == 1)
        {
            manualZone.Value = zones[0];
            if (Kind(source) != "utm") autoZone.Checked = false;
        }
        ambiguousSourceZones = explicitZone && zones.Count > 1;
        message.Text = ambiguousSourceZones
            ? "Разные зоны без префикса: для нового расчёта выберите исходную СК."
            : "Таблица в СК: " + Current(source)?.Name;
        UpdateZoneControls(); UpdateConvertButton();
    }

    // Snapshot used to reject changed CRS/zone settings after the asynchronous calculation.
    public string SettingsSnapshot() => BuildRequest(Array.Empty<SurveyPoint>());

    public string BuildRequest(IReadOnlyList<SurveyPoint> points)
    {
        if (Current(source) is not NativeCrsOption s || Current(target) is not NativeCrsOption t)
            throw new InvalidOperationException("Выберите исходную и целевую СК.");
        return CoordinateBridge.BuildRequest(points, s.Definition, t.Definition,
            new(autoZone.Checked, (double)manualZone.Value,
                (Kind(source) is "gk6" or "gk3") && sourcePrefix.Checked,
                (Kind(target) is "gk6" or "gk3") && targetPrefix.Checked));
    }
}
