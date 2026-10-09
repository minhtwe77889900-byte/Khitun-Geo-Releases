namespace KhitunGeo.Native;

internal static class NativeTheme
{
    public static void Apply(Control root, bool dark)
    {
        root.BackColor = dark ? Color.FromArgb(32, 34, 37) : Color.FromArgb(246, 247, 249);
        root.ForeColor = dark ? Color.Gainsboro : Color.FromArgb(30, 32, 36);
        if (root is DataGridView grid)
        {
            grid.EnableHeadersVisualStyles = false;
            grid.BackgroundColor = root.BackColor;
            grid.DefaultCellStyle.BackColor = root.BackColor;
            grid.DefaultCellStyle.ForeColor = root.ForeColor;
            grid.ColumnHeadersDefaultCellStyle.BackColor = dark ? Color.FromArgb(44, 46, 50) : Color.FromArgb(231, 234, 238);
            grid.ColumnHeadersDefaultCellStyle.ForeColor = root.ForeColor;
            grid.RowHeadersDefaultCellStyle = grid.ColumnHeadersDefaultCellStyle.Clone();
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(32, 110, 121);
            grid.DefaultCellStyle.SelectionForeColor = Color.White;
        }
        foreach (Control child in root.Controls) Apply(child, dark);
        root.Invalidate();
    }
}
