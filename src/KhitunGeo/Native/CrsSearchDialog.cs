namespace KhitunGeo.Native;

// The catalogue remains unchanged while the user searches. Only OK commits a choice.
internal sealed class CrsSearchDialog : Form
{
    private readonly IReadOnlyList<MskCrsEntry> entries;
    private readonly TextBox search = new() { Name = "CrsSearch", Dock = DockStyle.Fill, PlaceholderText = "Поиск: МСК-164, ГСК-2011, WGS…", Margin = new Padding(0, 4, 0, 8) };
    private readonly ListBox results = new() { Dock = DockStyle.Fill, DisplayMember = nameof(MskCrsEntry.Name), IntegralHeight = false, BorderStyle = BorderStyle.FixedSingle, HorizontalScrollbar = true, Margin = Padding.Empty };
    private readonly Label count = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Tag = "muted", Margin = Padding.Empty };
    private readonly Button choose = new() { Text = "Выбрать", Width = 120, Height = 36, Tag = "primary", DialogResult = DialogResult.OK };
    public MskCrsEntry? SelectedEntry => results.SelectedItem as MskCrsEntry;

    public CrsSearchDialog(IReadOnlyList<MskCrsEntry> catalogue, MskCrsEntry? current, bool dark)
    {
        entries = catalogue;
        Text = "Поиск системы координат";
        ClientSize = new Size(640, 470); MinimumSize = new Size(440, 360);
        StartPosition = FormStartPosition.CenterParent; ShowInTaskbar = false;
        Font = new Font("Segoe UI", 11); AutoScaleMode = AutoScaleMode.Dpi;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(20), ColumnCount = 1, RowCount = 5 };
        layout.ColumnStyles.Add(new(SizeType.Percent, 100));
        foreach (var h in new[] { 30, 48 }) layout.RowStyles.Add(new(SizeType.Absolute, h));
        layout.RowStyles.Add(new(SizeType.Percent, 100));
        foreach (var h in new[] { 30, 48 }) layout.RowStyles.Add(new(SizeType.Absolute, h));
        layout.Controls.Add(new Label { Text = "Система координат", Dock = DockStyle.Fill, Font = new Font(Font, FontStyle.Bold), Margin = Padding.Empty }, 0, 0);
        layout.Controls.Add(search, 0, 1); layout.Controls.Add(results, 0, 2); layout.Controls.Add(count, 0, 3);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Margin = Padding.Empty, Padding = new Padding(0, 6, 0, 0) };
        var cancel = new Button { Text = "Отмена", Width = 120, Height = 36, DialogResult = DialogResult.Cancel };
        buttons.Controls.Add(cancel); buttons.Controls.Add(choose); layout.Controls.Add(buttons, 0, 4);
        Controls.Add(layout); AcceptButton = choose; CancelButton = cancel;
        search.TextChanged += (_, _) => Filter();
        search.KeyDown += (_, e) => { if (e.KeyCode == Keys.Down && results.Items.Count > 0) { results.Focus(); e.Handled = true; } };
        results.SelectedIndexChanged += (_, _) => choose.Enabled = SelectedEntry is not null;
        results.DoubleClick += (_, _) => { if (SelectedEntry is not null) { DialogResult = DialogResult.OK; Close(); } };
        Filter(); results.SelectedItem = current;
        if (results.SelectedIndex < 0 && results.Items.Count > 0) results.SelectedIndex = 0;
        Shown += (_, _) => search.Focus(); NativeTheme.Apply(this, dark);
    }

    private static string Normalize(string value) => new(value.ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());
    private void Filter()
    {
        var query = Normalize(search.Text);
        var previous = SelectedEntry;
        results.BeginUpdate();
        try
        {
            results.Items.Clear();
            foreach (var entry in entries)
                if (query.Length == 0 || Normalize(entry.Name).Contains(query) || Normalize(entry.Id).Contains(query)) results.Items.Add(entry);
            if (previous is not null && results.Items.Contains(previous)) results.SelectedItem = previous;
            else if (results.Items.Count > 0) results.SelectedIndex = 0;
        }
        finally { results.EndUpdate(); }
        choose.Enabled = SelectedEntry is not null;
        count.Text = results.Items.Count == 0 ? "Ничего не найдено. Измените запрос." : $"Найдено систем: {results.Items.Count}";
    }
}

internal sealed class SearchableCrsPicker : ComboBox
{
    private bool opening;
    public SearchableCrsPicker()
    {
        FlatStyle = FlatStyle.Flat; DrawMode = DrawMode.OwnerDrawFixed; ItemHeight = 28;
    }
    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        using var background = new SolidBrush(BackColor); e.Graphics.FillRectangle(background, e.Bounds);
        var text = e.Index >= 0 && e.Index < Items.Count ? GetItemText(Items[e.Index]) : Text;
        TextRenderer.DrawText(e.Graphics, text, Font, Rectangle.Inflate(e.Bounds, -8, 0), ForeColor, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        if ((e.State & DrawItemState.Focus) != 0) e.DrawFocusRectangle();
    }
    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e); ItemHeight = Math.Max((int)(28 * DeviceDpi / 96f), Font.Height + (int)(8 * DeviceDpi / 96f));
    }
    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e); ItemHeight = Math.Max((int)(28 * DeviceDpi / 96f), Font.Height + (int)(8 * DeviceDpi / 96f));
    }
    private void OpenSearch()
    {
        if (opening || !IsHandleCreated || IsDisposed || !Enabled) return;
        opening = true;
        BeginInvoke(new Action(() =>
        {
            try
            {
                if (IsDisposed || Disposing || DataSource is not IReadOnlyList<MskCrsEntry> entries) return;
                using var dialog = new CrsSearchDialog(entries, SelectedItem as MskCrsEntry, NativeTheme.IsDark(this));
                if (UiMotion.ShowDialog(dialog, FindForm()) != DialogResult.OK || dialog.SelectedEntry is null) return;
                SelectedItem = dialog.SelectedEntry;
                OnSelectionChangeCommitted(EventArgs.Empty);
            }
            finally { opening = false; }
        }));
    }
    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x14F) // CB_SHOWDROPDOWN: intercept before Windows can create/show its list.
        {
            if (m.WParam != IntPtr.Zero) OpenSearch();
            m.Result = IntPtr.Zero; return;
        }
        if (m.Msg is 0x201 or 0x203) // Mouse click / double click.
        {
            Focus(); OpenSearch(); m.Result = IntPtr.Zero; return;
        }
        if (m.Msg is 0x100 or 0x104 && (Keys)m.WParam.ToInt32() is Keys.F4 or Keys.Space or Keys.Enter or Keys.Down or Keys.Up)
        {
            OpenSearch(); m.Result = IntPtr.Zero; return;
        }
        if (m.Msg == 0x20A) { m.Result = IntPtr.Zero; return; } // Wheel must not commit an unreviewed zone.
        if (m.Msg is 0x317 or 0x318 && m.WParam != IntPtr.Zero)
        {
            using var graphics = Graphics.FromHdc(m.WParam); PaintSelection(graphics);
            m.Result = new IntPtr(1); return;
        }
        base.WndProc(ref m);
        if (m.Msg == 0xF && IsHandleCreated) // Paint the arrow and border consistently in both themes.
        {
            using var graphics = Graphics.FromHwnd(Handle); PaintSelection(graphics);
        }
    }
    private void PaintSelection(Graphics graphics)
    {
        using var background = new SolidBrush(BackColor); graphics.FillRectangle(background, ClientRectangle);
        var scale = DeviceDpi / 96f; var arrowWidth = (int)(32 * scale);
        var textBounds = new Rectangle((int)(10 * scale), 0, Math.Max(1, Width - arrowWidth - (int)(16 * scale)), Height);
        TextRenderer.DrawText(graphics, Text, Font, textBounds, Enabled ? ForeColor : SystemColors.GrayText,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        using var border = new Pen(Focused ? Color.FromArgb(0, 147, 158) : NativeTheme.IsDark(this) ? Color.FromArgb(63, 78, 94) : Color.FromArgb(214, 221, 225));
        graphics.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
        using var arrow = new Pen(ForeColor, Math.Max(1, 1.5f * scale));
        var x = Width - arrowWidth / 2; var y = Height / 2;
        graphics.DrawLines(arrow, new[] { new PointF(x - 4 * scale, y - 2 * scale), new PointF(x, y + 2 * scale), new PointF(x + 4 * scale, y - 2 * scale) });
    }
}
