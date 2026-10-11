namespace KhitunGeo.Native;

internal sealed partial class NativeWorkspaceForm
{
    private readonly ToolTip navigationTips = new();
    private readonly List<(Button Button, string Caption, string Glyph)> navigationButtons = new();
    private bool navigationExpanded;
    private ThemeSwitcher? themeControl;
    private readonly ContextMenuStrip drawingMenu = new();
    private readonly ContextMenuStrip aboutMenu = new();
    private Button? drawingNavigationButton, aboutNavigationButton;
    private float navigationScale = 1;

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
        drawingMenu.Items.Add("Открыть DWG / DXF", null, async (_, _) => await ImportDrawing());
        drawingMenu.Items.Add("Слои и видимость", null, (_, _) => ShowDrawingLayers());
        drawingMenu.Items.Add("Убрать чертёж", null, (_, _) => ClearDrawing());
        drawingNavigationButton = Add(top, "Чертёж DWG / DXF", "\uE8A5", () => ShowNavigationMenu(drawingMenu, drawingNavigationButton!));
        visualizationSidebarButton = Add(top, "Скрыть визуализацию", "\uE890", ToggleVisualization);
        Add(top, "Изменить высоты Z", "\uE8CB", OffsetHeight);
        // Retain the existing passport/update buttons and their enable/cancel lifecycle.
        ConfigureExisting(passportButton, "\uE8A5", "Паспорт преобразования PDF", top);
        passportButton.Click += async (_, _) => await ExportPassportPdf();
        Add(bottom, "Настройки", "\uE713", OpenSettings);
        Add(bottom, "Проверить обновления", "\uE72C", async () => await CheckForUpdatesAsync(manual: true));
        aboutMenu.Items.Add("О программе", null, (_, _) => { using var about = new AboutForm(dark); UiMotion.ShowDialog(about, this); });
        aboutMenu.Items.Add("Диагностика", null, (_, _) => { using var diagnostics = new DiagnosticsForm(); UiMotion.ShowDialog(diagnostics, this); });
        aboutNavigationButton = Add(bottom, "О программе", "\uE946", () => ShowNavigationMenu(aboutMenu, aboutNavigationButton!));
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
        button.Text = caption; button.Font = Font;
        if (button is NavigationIconButton icon) icon.Glyph = glyph;
        button.Width = 56; button.Height = 48; button.Margin = new Padding(0, 3, 0, 3); button.AccessibleName = caption;
        NativeTheme.StyleNavigation(button); parent.Controls.Add(button);
        navigationButtons.Add((button, caption, glyph)); navigationTips.SetToolTip(button, caption);
    }

    private void ShowNavigationMenu(ContextMenuStrip menu, Button anchor)
    {
        menu.BackColor = dark ? Color.FromArgb(29, 46, 58) : Color.White;
        menu.ForeColor = dark ? Color.FromArgb(229, 238, 240) : Color.FromArgb(17, 27, 87);
        menu.Show(anchor, new Point(anchor.Width, 0));
    }

    private void ToggleNavigation(Control rail)
    {
        // Derive scale only at rest; interruption continues from the current width.
        if (!navigationAnimating) navigationScale = rail.Width / (navigationExpanded ? 224f : 76f);
        navigationExpanded = !navigationExpanded;
        var start = rail.Width; var finish = (int)((navigationExpanded ? 224 : 76) * navigationScale);
        navigationAnimating = true;
        UiMotion.Animate(rail, 210, t =>
        {
            rail.SuspendLayout();
            rail.Width = (int)Math.Round(start + (finish - start) * t);
            foreach (var item in navigationButtons) item.Button.Width = Math.Max((int)(56 * navigationScale), rail.ClientSize.Width - rail.Padding.Horizontal);
            rail.ResumeLayout(true);
        }, () => navigationAnimating = false);
    }
    private bool navigationAnimating;

    private void AddPoint()
    {
        if (grid.EndEdit()) workspace.ReplacePoints(workspace.Points.Append(new SurveyPoint((workspace.Points.Count + 1).ToString(), null, null, null, "")).ToArray());
    }
}

internal sealed class NavigationIconButton : Button
{
    public string Glyph { get; set; } = "";
    public bool Selected { get; init; }
    private double hoverAmount;
    public NavigationIconButton()
    {
        SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = Color.Transparent; UseVisualStyleBackColor = false;
    }
    private void AnimateHover(double target)
    {
        var start = hoverAmount;
        UiMotion.Animate(this, 130, t => { hoverAmount = start + (target - start) * t; Invalidate(); });
    }
    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); AnimateHover(1); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); AnimateHover(0); }
    protected override void OnPaintBackground(PaintEventArgs e) => BrandArtwork.PaintBackdrop(this, e.Graphics);
    protected override void OnPaint(PaintEventArgs e)
    {
        var dark = NativeTheme.IsDark(this); var scale = DeviceDpi / 96f;
        var accent = dark ? Color.FromArgb(62, 214, 220) : Color.FromArgb(0, 129, 144);
        var alpha = (int)(Selected ? (dark ? 37 : 26) : hoverAmount * (dark ? 32 : 22));
        if (alpha > 0)
        {
            using var brush = new SolidBrush(Color.FromArgb(alpha, accent)); e.Graphics.FillRectangle(brush, ClientRectangle);
        }
        if (Selected) { using var line = new SolidBrush(accent); e.Graphics.FillRectangle(line, 0, 3, 3 * scale, Height - 6); }
        using var glyphFont = new Font("Segoe MDL2 Assets", Font.Size * 1.8f);
        var iconWidth = Math.Min(Width, (int)(56 * scale));
        var ink = Enabled ? (Selected ? accent : ForeColor) : (dark ? Color.FromArgb(121, 145, 159) : Color.FromArgb(143, 154, 171));
        const TextFormatFlags flags = TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.PreserveGraphicsClipping | TextFormatFlags.PreserveGraphicsTranslateTransform;
        TextRenderer.DrawText(e.Graphics, Glyph, glyphFont, new Rectangle(0, 0, iconWidth, Height), ink, flags | TextFormatFlags.HorizontalCenter);
        if (Width > 100 * scale)
            TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle((int)(56 * scale), 0, Width - (int)(60 * scale), Height), ink, flags | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        if (Focused) ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -5, -5));
    }
}
