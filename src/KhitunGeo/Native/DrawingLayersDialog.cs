namespace KhitunGeo.Native;

internal sealed class DrawingLayersDialog : Form
{
    private readonly CheckedListBox list = new() { Dock = DockStyle.Fill, CheckOnClick = true, HorizontalScrollbar = true };
    public IReadOnlySet<string> VisibleLayers => list.CheckedItems.Cast<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);
    public DrawingLayersDialog(DrawingScene scene, IReadOnlySet<string> visibleLayers, bool dark)
    {
        Text = "Слои чертежа"; Width = 460; Height = 520; MinimumSize = new Size(340, 260);
        StartPosition = FormStartPosition.CenterParent; MinimizeBox = false; MaximizeBox = false;
        foreach (var layer in scene.Layers.OrderBy(l => l.Name, StringComparer.OrdinalIgnoreCase)) list.Items.Add(layer.Name, visibleLayers.Contains(layer.Name));
        var actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 90, AutoScroll = true, Padding = new Padding(4) };
        void Toggle(string label, bool value)
        {
            var button = new Button { Text = label, AutoSize = true };
            button.Click += (_, _) => { for (var i = 0; i < list.Items.Count; i++) list.SetItemChecked(i, value); };
            actions.Controls.Add(button);
        }
        Toggle("Показать все", true); Toggle("Скрыть все", false);
        var apply = new Button { Text = "Применить", AutoSize = true, DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "Отмена", AutoSize = true, DialogResult = DialogResult.Cancel };
        actions.Controls.Add(apply); actions.Controls.Add(cancel); AcceptButton = apply; CancelButton = cancel;
        Controls.Add(list); Controls.Add(actions); NativeTheme.Apply(this, dark);
    }
}
