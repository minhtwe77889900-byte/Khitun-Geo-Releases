namespace KhitunGeo.Native;

internal sealed class MskZoneDialog : Form
{
    private readonly ListBox zones = new() { BindingContext = new BindingContext(), Dock = DockStyle.Fill, DisplayMember = nameof(NativeCrsOption.Name), IntegralHeight = false, HorizontalScrollbar = true };
    public NativeCrsOption? SelectedZone => zones.SelectedItem as NativeCrsOption;

    public MskZoneDialog(MskCrsEntry entry, NativeCrsOption current, bool dark)
    {
        Text = "Выбор зоны — " + entry.Name;
        Size = new Size(640, 380); MinimumSize = new Size(480, 300);
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Segoe UI", 10); AutoScaleMode = AutoScaleMode.Dpi;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 1, RowCount = 3 };
        layout.ColumnStyles.Add(new(SizeType.Percent, 100));
        layout.RowStyles.Add(new(SizeType.Absolute, 40)); layout.RowStyles.Add(new(SizeType.Percent, 100)); layout.RowStyles.Add(new(SizeType.Absolute, 48));
        layout.Controls.Add(new Label { Text = entry.Name, Dock = DockStyle.Fill, Font = new Font(Font, FontStyle.Bold) }, 0, 0);
        zones.DataSource = entry.Zones.ToList(); zones.SelectedItem = current;
        layout.Controls.Add(zones, 0, 1);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 8, 0, 0) };
        var cancel = new Button { Text = "Отмена", AutoSize = true, DialogResult = DialogResult.Cancel };
        var ok = new Button { Text = "Выбрать зону", AutoSize = true, DialogResult = DialogResult.OK, Tag = "primary" };
        buttons.Controls.Add(cancel); buttons.Controls.Add(ok); layout.Controls.Add(buttons, 0, 2);
        zones.DoubleClick += (_, _) => { if (SelectedZone is not null) { DialogResult = DialogResult.OK; Close(); } };
        Controls.Add(layout); AcceptButton = ok; CancelButton = cancel;
        NativeTheme.Apply(this, dark);
    }
}
