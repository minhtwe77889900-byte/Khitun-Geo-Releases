namespace KhitunGeo.Native;

internal sealed partial class NativeWorkspaceForm
{
    private readonly ToolTip navigationTips = new();
    private readonly List<(Button Button, string Caption, string Glyph)> navigationButtons = new();
    private bool navigationExpanded;
    private ThemeSwitcher? themeControl;

    private Control BuildNavigation()
    {
        var navigation = new BrandPanel { Name = "NavigationRail", Dock = DockStyle.Left, Width = 76, Padding = new Padding(10, 0, 10, 12) };
        var top = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Margin = Padding.Empty, Tag = "brand-overlay" };
        var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 260, FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = Padding.Empty, Tag = "brand-overlay" };
        Button Add(Control parent, string caption, string glyph, Action action, bool selected = false)
        {
            var button = new NavigationIconButton { Text = caption, Glyph = glyph, Selected = selected, Width = 56, Height = 48, Margin = new Padding(0, 3, 0, 3), AccessibleName = caption };
            NativeTheme.StyleNavigation(button); button.Click += (_, _) => action();
            parent.Controls.Add(button); navigationButtons.Add((button, caption, glyph)); navigationTips.SetToolTip(button, caption); return button;
        }
        Add(top, "Точки и таблица", "\uE80A", () => grid.Focus(), true);
        Add(top, "Чертёж DWG / DXF", "\uE8A5", () =>
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add("Открыть DWG / DXF", null, async (_, _) => await ImportDrawing());
            menu.Items.Add("Слои и видимость", null, (_, _) => ShowDrawingLayers());
            menu.Items.Add("Убрать чертёж", null, (_, _) => ClearDrawing());
            menu.Closed += (_, _) => menu.Dispose(); menu.Show(Cursor.Position);
        });
        visualizationSidebarButton = Add(top, "Скрыть визуализацию", "\uE890", ToggleVisualization);
        Add(top, "Изменить высоты Z", "\uE8CB", OffsetHeight);
        // Retain the existing passport/update buttons and their enable/cancel lifecycle.
        ConfigureExisting(passportButton, "\uE8A5", "Паспорт преобразования PDF", top);
        passportButton.Click += async (_, _) => await ExportPassportPdf();
        Add(bottom, "Настройки", "\uE713", OpenSettings);
        Add(bottom, "Проверить обновления", "\uE72C", async () => await CheckForUpdatesAsync(manual: true));
        Add(bottom, "О программе", "\uE946", () => { using var about = new AboutForm(dark); about.ShowDialog(this); });
        themeControl = new ThemeSwitcher { Name = "ThemeSwitcher", Width = 52, Height = 94, Margin = new Padding(2, 5, 2, 0) };
        themeControl.ThemeChanged += (_, _) => { dark = themeControl.Dark; ThemeTransition.Apply(this, dark); };
        bottom.Controls.Add(themeControl);
        ConfigureExisting(installUpdateButton, "\uE896", "Установить обновление", top);
        installUpdateButton.Click += async (_, _) => await InstallUpdateAsync();
        ConfigureExisting(cancelUpdateButton, "\uE711", "Отменить обновление", top);
        cancelUpdateButton.Click += (_, _) => updateOperation?.Cancel();
        navigation.Controls.Add(top); navigation.Controls.Add(bottom);
        return navigation;
    }

    private void ConfigureExisting(Button button, string glyph, string caption, Control parent)
    {
        button.Text = glyph; button.Font = new Font("Segoe MDL2 Assets", 18);
        button.Width = 56; button.Height = 48; button.Margin = new Padding(0, 3, 0, 3); button.AccessibleName = caption;
        NativeTheme.StyleNavigation(button); parent.Controls.Add(button);
        navigationButtons.Add((button, caption, glyph)); navigationTips.SetToolTip(button, caption);
    }

    private void ToggleNavigation(Control rail)
    {
        navigationExpanded = !navigationExpanded;
        var dpi = DeviceDpi / 96f;
        rail.Width = (int)((navigationExpanded ? 224 : 76) * dpi);
        foreach (var item in navigationButtons)
        {
            item.Button.Width = (int)((navigationExpanded ? 204 : 56) * dpi);
            if (item.Button is not NavigationIconButton)
            {
                item.Button.Text = navigationExpanded ? item.Caption : item.Glyph;
                item.Button.Font = new Font(navigationExpanded ? "Segoe UI" : "Segoe MDL2 Assets", navigationExpanded ? 9 : 18);
            }
            item.Button.Invalidate();
        }
        if (themeControl is not null) themeControl.Width = (int)((navigationExpanded ? 200 : 52) * dpi);
    }

    private void AddPoint()
    {
        if (grid.EndEdit()) workspace.ReplacePoints(workspace.Points.Append(new SurveyPoint((workspace.Points.Count + 1).ToString(), null, null, null, "")).ToArray());
    }
}

internal sealed class NavigationIconButton : Button
{
    public string Glyph { get; init; } = "";
    public bool Selected { get; init; }
    private bool hover;
    public NavigationIconButton() { DoubleBuffered = true; }
    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnPaint(PaintEventArgs e)
    {
        var dark = NativeTheme.IsDark(this); var scale = DeviceDpi / 96f;
        e.Graphics.Clear(BackColor);
        var accent = dark ? Color.FromArgb(62, 214, 220) : Color.FromArgb(0, 129, 144);
        if (Selected || hover)
        {
            using var brush = new SolidBrush(Color.FromArgb(dark ? 37 : 26, accent)); e.Graphics.FillRectangle(brush, ClientRectangle);
        }
        if (Selected) { using var line = new SolidBrush(accent); e.Graphics.FillRectangle(line, 0, 3, 3 * scale, Height - 6); }
        using var glyphFont = new Font("Segoe MDL2 Assets", 18);
        TextRenderer.DrawText(e.Graphics, Glyph, glyphFont, new Rectangle(0, 0, (int)(56 * scale), Height), Selected ? accent : ForeColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        if (Width > 100 * scale)
            TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle((int)(56 * scale), 0, Width - (int)(60 * scale), Height), ForeColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        if (Focused) ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -5, -5));
    }
}
