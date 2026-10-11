namespace KhitunGeo.Native;

internal static class NativeTheme
{
    private static readonly Color Accent = Color.FromArgb(0, 127, 141);
    public static bool IsDark(Control control)
    {
        while (control.BackColor.A < 255 && control.Parent is not null) control = control.Parent;
        return control.BackColor.GetBrightness() < .4;
    }
    public static void StyleNavigation(Button button)
    {
        button.Tag = "navigation";
        button.FlatAppearance.BorderSize = 0;
        button.Cursor = Cursors.Hand;
    }

    public static void Apply(Control root, bool dark)
    {
        var surface = dark ? Color.FromArgb(29, 46, 58) : Color.White;
        var canvas = dark ? Color.FromArgb(20, 29, 37) : Color.FromArgb(244, 245, 241);
        var ink = dark ? Color.FromArgb(229, 238, 240) : Color.FromArgb(17, 27, 87);
        var border = dark ? Color.FromArgb(63, 78, 94) : Color.FromArgb(214, 221, 225);
        root.BackColor = root is Form or SplitContainer or BrandPanel or BrandLayoutPanel or BrandNavigationPanel || root.Tag as string == "navigation" ? canvas : surface;
        if (root.Tag as string == "brand-overlay") root.BackColor = Color.Transparent;
        // These controls paint the exact ancestor artwork themselves. Keep their
        // HWND background opaque to avoid WinForms replaying sibling foregrounds.
        if (root is NavigationIconButton or ThemeSwitcher) root.BackColor = canvas;
        root.ForeColor = ink;
        if (root.Tag as string == "muted") root.ForeColor = dark ? Color.FromArgb(155, 176, 195) : Color.FromArgb(111, 131, 153);
        if (root.Tag as string == "hint") { root.BackColor = dark ? Color.FromArgb(25, 62, 62) : Color.FromArgb(235, 247, 245); root.ForeColor = dark ? Color.FromArgb(136, 224, 210) : Accent; }
        if (root is Button button)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = border;
            button.Cursor = Cursors.Hand;
            button.UseVisualStyleBackColor = false;
            if (button.Tag as string == "primary") { button.BackColor = dark ? Color.FromArgb(9, 158, 170) : Accent; button.ForeColor = Color.White; button.FlatAppearance.BorderSize = 0; }
            else if (button.Tag as string == "navigation") { if (button is not NavigationIconButton) button.BackColor = canvas; button.FlatAppearance.BorderSize = 0; }
            else if (button.Tag as string == "zone") { button.FlatAppearance.BorderSize = 0; }
            else if (button.Tag as string == "accent-outline") { button.FlatAppearance.BorderSize = 1; button.FlatAppearance.BorderColor = Accent; button.ForeColor = dark ? Color.FromArgb(66, 216, 221) : Accent; }
            else { button.FlatAppearance.BorderSize = 1; }
            button.FlatAppearance.MouseOverBackColor = button.Tag as string == "primary" ? Color.FromArgb(6, 108, 97) : dark ? Color.FromArgb(45, 65, 77) : Color.FromArgb(226, 241, 237);
        }
        if (root is DataGridView grid)
        {
            grid.EnableHeadersVisualStyles = false;
            grid.BackgroundColor = surface; grid.BorderStyle = BorderStyle.None;
            grid.GridColor = border;
            grid.DefaultCellStyle.BackColor = surface; grid.DefaultCellStyle.ForeColor = ink;
            grid.AlternatingRowsDefaultCellStyle.BackColor = dark ? Color.FromArgb(24, 40, 51) : Color.FromArgb(246, 248, 248);
            grid.ColumnHeadersDefaultCellStyle.BackColor = dark ? Color.FromArgb(26, 43, 54) : Color.FromArgb(242, 244, 245);
            grid.ColumnHeadersDefaultCellStyle.ForeColor = ink;
            if (grid.ColumnHeadersDefaultCellStyle.Font is null || !grid.ColumnHeadersDefaultCellStyle.Font.Bold)
                grid.ColumnHeadersDefaultCellStyle.Font = new Font(grid.Font, FontStyle.Bold);
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            grid.ColumnHeadersHeight = (int)(40 * grid.DeviceDpi / 96f);
            grid.RowTemplate.Height = (int)(40 * grid.DeviceDpi / 96f);
            grid.CellBorderStyle = DataGridViewCellBorderStyle.Single;
            grid.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            if (grid is PointGrid && grid.Columns.Count > 4) grid.Columns[4].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
            grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            grid.RowHeadersDefaultCellStyle = grid.ColumnHeadersDefaultCellStyle.Clone();
            grid.DefaultCellStyle.SelectionBackColor = dark ? Color.FromArgb(32, 90, 87) : Color.FromArgb(220, 240, 235);
            grid.DefaultCellStyle.SelectionForeColor = ink;
        }
        if (root is PointPreview) root.BackColor = dark ? Color.FromArgb(17, 32, 41) : Color.White;
        if (root is ThemeSwitcher switcher) switcher.CommitVisualState();
        foreach (Control child in root.Controls) Apply(child, dark);
        root.Invalidate();
    }
}
