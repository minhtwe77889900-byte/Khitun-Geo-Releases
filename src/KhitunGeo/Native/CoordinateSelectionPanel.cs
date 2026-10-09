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

    private readonly Button convert = new() { Text = "Преобразовать", Dock = DockStyle.Fill, Enabled = false };
    private readonly Label message = new() { Text = "Расчёт недоступен", AutoSize = true, Dock = DockStyle.Fill };
    private bool catalogueReady, runtimeReady, busy, ambiguousSourceZones;
    public event EventHandler? ConvertRequested;
    public bool UsesAutoTargetZone => autoZone.Checked && Kind(target) is "gk6" or "gk3" or "utm";
    public bool SourceIsGeographic => (Kind(source) ?? throw new InvalidOperationException("Выберите исходную систему координат.")) == "geo";
    public string SourceName => (source.SelectedItem as NativeCrsOption)?.Name ?? "";
    public string TargetName => (target.SelectedItem as NativeCrsOption)?.Name ?? "";

    public bool SelectSource(string id)
    {
        if (source.DataSource is not IEnumerable<NativeCrsOption> systems) return false;
        var option = systems.FirstOrDefault(item => string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase));
        if (option is null) return false;
        source.SelectedItem = option;
        return true;
    }

    public CoordinateSelectionPanel(string cataloguePath, Action swapPointAxes)
    {
        Dock = DockStyle.Top; Height = 150; ColumnCount = 4; RowCount = 4; Padding = new Padding(10, 5, 10, 5);
        ColumnStyles.Add(new(SizeType.Percent, 50)); ColumnStyles.Add(new(SizeType.Absolute, 48));
        ColumnStyles.Add(new(SizeType.Percent, 50)); ColumnStyles.Add(new(SizeType.Absolute, 150));
        RowStyles.Add(new(SizeType.Absolute, 23)); RowStyles.Add(new(SizeType.Absolute, 34));
        RowStyles.Add(new(SizeType.Absolute, 55)); RowStyles.Add(new(SizeType.Absolute, 24));
        Controls.Add(new Label { Text = "Координаты в таблице", AutoSize = true }, 0, 0);
        Controls.Add(new Label { Text = "Преобразовать в", AutoSize = true }, 2, 0);
        Controls.Add(source, 0, 1); Controls.Add(target, 2, 1);
        var swap = new Button { Text = "X↔Y", Dock = DockStyle.Fill };
        swap.Click += (_, _) => swapPointAxes(); Controls.Add(swap, 1, 1);
        convert.Click += (_, _) => ConvertRequested?.Invoke(this, EventArgs.Empty);
        Controls.Add(convert, 3, 1);
        var options = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true };
        options.Controls.Add(autoZone); options.Controls.Add(sourcePrefix); options.Controls.Add(targetPrefix);
        options.Controls.Add(new Label { Text = "Зона", AutoSize = true, Margin = new Padding(8, 5, 3, 0) });
        options.Controls.Add(manualZone); Controls.Add(options, 0, 2); SetColumnSpan(options, 4);
        Controls.Add(message, 0, 3); SetColumnSpan(message, 4);
        source.SelectedIndexChanged += (_, _) => { ambiguousSourceZones = false; UpdateZoneControls(); UpdateConvertButton(); };
        target.SelectedIndexChanged += (_, _) => UpdateZoneControls();
        autoZone.CheckedChanged += (_, _) => UpdateZoneControls();
        try
        {
            var systems = NativeCrsCatalogue.Read(cataloguePath);
            source.DataSource = systems.ToList(); target.DataSource = systems.ToList();
            source.SelectedItem = systems.First(c => c.Id == "wgs");
            target.SelectedItem = systems.First(c => c.Id == "msk164");
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
        Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, DropDownWidth = 620,
        DisplayMember = nameof(NativeCrsOption.Name), ValueMember = nameof(NativeCrsOption.Id),
        AutoCompleteSource = AutoCompleteSource.ListItems, AutoCompleteMode = AutoCompleteMode.SuggestAppend
    };

    private string? Kind(ComboBox picker) => (picker.SelectedItem as NativeCrsOption)?.Definition.GetProperty("kind").GetString();

    private void UpdateZoneControls()
    {
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

    public Action CaptureUndo()
    {
        var s = source.SelectedItem; var t = target.SelectedItem;
        var auto = autoZone.Checked; var zone = manualZone.Value;
        var sp = sourcePrefix.Checked; var tp = targetPrefix.Checked;
        var ambiguous = ambiguousSourceZones; var text = message.Text;
        return () =>
        {
            source.SelectedItem = s; target.SelectedItem = t;
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
        source.SelectedItem = target.SelectedItem;
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
            : "Таблица в СК: " + (source.SelectedItem as NativeCrsOption)?.Name;
        UpdateZoneControls(); UpdateConvertButton();
    }

    // Snapshot used to reject changed CRS/zone settings after the asynchronous calculation.
    public string SettingsSnapshot() => BuildRequest(Array.Empty<SurveyPoint>());

    public string BuildRequest(IReadOnlyList<SurveyPoint> points)
    {
        if (source.SelectedItem is not NativeCrsOption s || target.SelectedItem is not NativeCrsOption t)
            throw new InvalidOperationException("Выберите исходную и целевую СК.");
        return CoordinateBridge.BuildRequest(points, s.Definition, t.Definition,
            new(autoZone.Checked, (double)manualZone.Value,
                (Kind(source) is "gk6" or "gk3") && sourcePrefix.Checked,
                (Kind(target) is "gk6" or "gk3") && targetPrefix.Checked));
    }
}
