namespace KhitunGeo.Native;

internal static class NativeTheme
{
    private static readonly Color Accent = Color.FromArgb(8, 127, 115);
    public static void StyleNavigation(Button button)
    {
        button.Tag = "navigation";
        button.FlatAppearance.BorderSize = 0;
        button.Cursor = Cursors.Hand;
    }

    public static void Apply(Control root, bool dark)
    {
        var surface = dark ? Color.FromArgb(32, 40, 50) : Color.White;
        var canvas = dark ? Color.FromArgb(23, 30, 40) : Color.FromArgb(244, 247, 251);
        var ink = dark ? Color.FromArgb(225, 233, 243) : Color.FromArgb(27, 47, 70);
        var border = dark ? Color.FromArgb(63, 78, 94) : Color.FromArgb(214, 224, 235);
        root.BackColor = root is Form or SplitContainer || root.Tag as string == "navigation" ? canvas : surface;
        root.ForeColor = ink;
        if (root.Tag as string == "muted") root.ForeColor = dark ? Color.FromArgb(155, 176, 195) : Color.FromArgb(111, 131, 153);
        if (root.Tag as string == "hint") { root.BackColor = dark ? Color.FromArgb(25, 62, 62) : Color.FromArgb(235, 247, 245); root.ForeColor = dark ? Color.FromArgb(136, 224, 210) : Accent; }
        if (root is Button button)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = border;
            button.Cursor = Cursors.Hand;
            button.UseVisualStyleBackColor = false;
            if (button.Tag as string == "primary") { button.BackColor = Accent; button.ForeColor = Color.White; button.FlatAppearance.BorderSize = 0; }
            else if (button.Tag as string == "navigation") { button.BackColor = canvas; button.FlatAppearance.BorderSize = 0; }
            else { button.FlatAppearance.BorderSize = 1; }
            button.FlatAppearance.MouseOverBackColor = button.Tag as string == "primary" ? Color.FromArgb(6, 108, 97) : dark ? Color.FromArgb(45, 65, 77) : Color.FromArgb(226, 241, 237);
        }
        if (root is DataGridView grid)
        {
            grid.EnableHeadersVisualStyles = false;
            grid.BackgroundColor = surface; grid.BorderStyle = BorderStyle.None;
            grid.GridColor = border;
            grid.DefaultCellStyle.BackColor = surface; grid.DefaultCellStyle.ForeColor = ink;
            grid.AlternatingRowsDefaultCellStyle.BackColor = dark ? Color.FromArgb(29, 38, 48) : Color.FromArgb(249, 251, 253);
            grid.ColumnHeadersDefaultCellStyle.BackColor = dark ? Color.FromArgb(44, 56, 69) : Color.FromArgb(239, 244, 249);
            grid.ColumnHeadersDefaultCellStyle.ForeColor = ink;
            if (grid.ColumnHeadersDefaultCellStyle.Font is null || !grid.ColumnHeadersDefaultCellStyle.Font.Bold)
                grid.ColumnHeadersDefaultCellStyle.Font = new Font(grid.Font, FontStyle.Bold);
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            grid.ColumnHeadersHeight = (int)(38 * grid.DeviceDpi / 96f);
            grid.RowTemplate.Height = (int)(34 * grid.DeviceDpi / 96f);
            grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            grid.RowHeadersDefaultCellStyle = grid.ColumnHeadersDefaultCellStyle.Clone();
            grid.DefaultCellStyle.SelectionBackColor = dark ? Color.FromArgb(32, 90, 87) : Color.FromArgb(220, 240, 235);
            grid.DefaultCellStyle.SelectionForeColor = ink;
        }
        foreach (Control child in root.Controls) Apply(child, dark);
        root.Invalidate();
    }
}
