using System.Globalization;

namespace KhitunGeo.Native;

// Explicit source interpretation only; projection mathematics is unchanged.
internal sealed class ZonedCrsDialog : Form
{
    private readonly NumericUpDown number = new() { Name = "ZoneNumber", Minimum = 1, Width = 92 };
    private readonly CheckBox prefix = new ThemedCheckBox { Name = "ZonePrefix", AutoSize = false, Dock = DockStyle.Fill };
    private readonly Label range = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
    private readonly Label example = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Tag = "muted" };
    public int SelectedZone => (int)number.Value;
    public bool SourcePrefix => prefix.Enabled && prefix.Checked;

    public ZonedCrsDialog(string name, string kind, int zone, bool sourcePrefix, bool dark, int maximum)
    {
        Text = "Исходная зона — " + name;
        ClientSize = new Size(620, 290); MinimumSize = new Size(560, 300);
        StartPosition = FormStartPosition.CenterParent; ShowInTaskbar = false;
        Font = new Font("Segoe UI", 10); AutoScaleMode = AutoScaleMode.Dpi;
        number.Maximum = Math.Min(maximum, kind == "gk3" ? 120 : 60);
        number.Value = Math.Clamp(zone, 1, (int)number.Maximum);
        prefix.Text = "Y содержит номер зоны (миллионный префикс)";
        prefix.Enabled = kind != "utm"; prefix.Checked = sourcePrefix && prefix.Enabled;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(20), ColumnCount = 1, RowCount = 6 };
        layout.ColumnStyles.Add(new(SizeType.Percent, 100));
        foreach (var height in new[] { 34, 42, 36, 42, 48 }) layout.RowStyles.Add(new(SizeType.Absolute, height));
        layout.RowStyles.Add(new(SizeType.Percent, 100));
        layout.Controls.Add(new Label { Text = name, Dock = DockStyle.Fill, AutoEllipsis = true, Font = new Font(Font, FontStyle.Bold) }, 0, 0);
        var zoneRow = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        zoneRow.Controls.Add(new Label { Text = "Номер зоны", AutoSize = true, Margin = new Padding(0, 5, 16, 0) });
        zoneRow.Controls.Add(number); layout.Controls.Add(zoneRow, 0, 1);
        layout.Controls.Add(range, 0, 2); layout.Controls.Add(prefix, 0, 3); layout.Controls.Add(example, 0, 4);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        var cancel = new Button { Text = "Отмена", Width = 120, Height = 34, DialogResult = DialogResult.Cancel };
        var choose = new Button { Text = "Применить", Width = 120, Height = 34, Tag = "primary", DialogResult = DialogResult.OK };
        buttons.Controls.Add(cancel); buttons.Controls.Add(choose); layout.Controls.Add(buttons, 0, 5);
        void Describe()
        {
            range.Text = ZoneDescription(kind, SelectedZone);
            example.Text = SourcePrefix
                ? $"Пример Y с префиксом зоны {SelectedZone}: {SelectedZone * 1000000L + 599112},246"
                : "Пример Y без префикса: 599112,246. Используется выбранная зона.";
        }
        number.ValueChanged += (_, _) => Describe(); prefix.CheckedChanged += (_, _) => Describe();
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
