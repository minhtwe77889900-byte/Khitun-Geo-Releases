using System.Globalization;

namespace KhitunGeo.Native;

// The coordinate adapter uses one zone mode and number for both projections.
internal sealed class ZonedCrsDialog : Form
{
    private readonly NumericUpDown number = new() { Name = "ZoneNumber", Minimum = 1, Width = 92 };
    private readonly CheckBox auto = new ThemedCheckBox { Name = "ZoneAuto", Dock = DockStyle.Fill };
    private readonly CheckBox prefix = new ThemedCheckBox { Name = "ZonePrefix", AutoSize = false, Dock = DockStyle.Fill };
    private readonly Label range = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
    private readonly Label example = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Tag = "muted" };
    public int SelectedZone => (int)number.Value;
    public bool HasPrefix => prefix.Checked;
    public bool Automatic => auto.Checked;

    public ZonedCrsDialog(string name, string kind, int zone, bool automatic, bool hasPrefix,
        bool isSource, bool dark, int maximum, bool unprefixedSource)
    {
        Text = (isSource ? "Исходная зона — " : "Целевая зона — ") + name;
        ClientSize = new Size(600, 320); MinimumSize = new Size(420, 325);
        StartPosition = FormStartPosition.CenterParent; ShowInTaskbar = false;
        Font = new Font("Segoe UI", 10); AutoScaleMode = AutoScaleMode.Dpi;
        number.Maximum = Math.Min(maximum, kind == "gk3" ? 120 : 60);
        number.Value = Math.Clamp(zone, 1, (int)number.Maximum);
        prefix.Text = isSource ? "Исходный Y содержит номер зоны" : "Добавить номер зоны к целевому Y";
        prefix.Checked = hasPrefix && kind != "utm"; prefix.Visible = kind != "utm";
        auto.Text = "Автозона для преобразования";
        auto.Checked = automatic;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 1, RowCount = 7 };
        layout.ColumnStyles.Add(new(SizeType.Percent, 100));
        foreach (var height in new[] { 30, 32, 36, 32, 32 }) layout.RowStyles.Add(new(SizeType.Absolute, height));
        layout.RowStyles.Add(new(SizeType.Percent, 100)); layout.RowStyles.Add(new(SizeType.Absolute, 46));
        layout.Controls.Add(new Label { Text = name, Dock = DockStyle.Fill, AutoEllipsis = true, Font = new Font(Font, FontStyle.Bold) }, 0, 0);
        layout.Controls.Add(auto, 0, 1);
        var zoneRow = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        zoneRow.Controls.Add(new Label { Text = "Номер зоны", AutoSize = true, Margin = new Padding(0, 5, 16, 0) });
        zoneRow.Controls.Add(number); layout.Controls.Add(zoneRow, 0, 2);
        layout.Controls.Add(range, 0, 3); layout.Controls.Add(prefix, 0, 4); layout.Controls.Add(example, 0, 5);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        var cancel = new Button { Text = "Отмена", Width = 120, Height = 34, DialogResult = DialogResult.Cancel };
        var choose = new Button { Text = "Применить", Width = 120, Height = 34, Tag = "primary", DialogResult = DialogResult.OK };
        buttons.Controls.Add(cancel); buttons.Controls.Add(choose); layout.Controls.Add(buttons, 0, 6);
        void Describe()
        {
            auto.Enabled = isSource ? kind == "utm" || prefix.Checked : !unprefixedSource;
            if (!auto.Enabled) auto.Checked = false;
            number.Enabled = !Automatic || (isSource && kind == "utm");
            range.Text = number.Enabled ? ZoneDescription(kind, SelectedZone)
                : isSource ? "Зона определяется по миллионному префиксу Y." : "Зона определяется по долготе каждой точки.";
            example.Text = Automatic && isSource && kind == "utm"
                ? "Для исходного UTM укажите номер зоны. Автозона применяется к целевой проекции."
                : !isSource && unprefixedSource ? "Для исходного Y без префикса требуется ручная зона."
                : prefix.Checked ? $"Пример Y с префиксом зоны {SelectedZone}: {SelectedZone * 1000000L + 599112},246"
                : "Пример Y без префикса: 599112,246.";
        }
        number.ValueChanged += (_, _) => Describe(); prefix.CheckedChanged += (_, _) => Describe(); auto.CheckedChanged += (_, _) => Describe();
        Describe(); Controls.Add(layout); AcceptButton = choose; CancelButton = cancel;
        NativeTheme.Apply(this, dark);
    }

    internal static string ZoneDescription(string kind, int zone)
    {
        double width = kind == "gk3" ? 3 : 6;
        double center = kind == "utm" ? zone * 6 - 183 : kind == "gk3" ? zone * 3 : zone * 6 - 3;
        string Format(double value) => value.ToString("0.#", CultureInfo.InvariantCulture);
        return $"Зона {zone}: {Format(center - width / 2)}–{Format(center + width / 2)}° · меридиан {Format(center)}°";
    }
}
