using System.Drawing.Drawing2D;

namespace KhitunGeo.Native;

internal sealed class ThemeSwitcher : Control
{
    public bool Dark { get; private set; }
    public event EventHandler? ThemeChanged;
    public ThemeSwitcher()
    {
        Size = new Size(160, 40); Cursor = Cursors.Hand; AccessibleName = "Светлая или тёмная тема";
        SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        TabStop = true;
    }
    public void SetDark(bool value)
    {
        if (Dark == value) return;
        Dark = value; Invalidate(); ThemeChanged?.Invoke(this, EventArgs.Empty);
    }
    protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); Focus(); SetDark(e.X >= Width / 2); }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode is Keys.Space or Keys.Left or Keys.Right) { SetDark(e.KeyCode == Keys.Left ? false : e.KeyCode == Keys.Right ? true : !Dark); e.Handled = true; }
    }
    private static GraphicsPath Capsule(Rectangle bounds)
    {
        var path = new GraphicsPath(); var diameter = Math.Min(bounds.Height, bounds.Width);
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 90, 180);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 180);
        path.CloseFigure(); return path;
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var border = new Pen(Dark ? Color.FromArgb(54, 185, 174) : Color.FromArgb(8, 127, 115));
        using var background = new SolidBrush(Dark ? Color.FromArgb(24, 53, 59) : Color.FromArgb(226, 241, 235));
        var half = Width / 2; var active = new Rectangle(Dark ? half : 2, 2, half - 4, Height - 5);
        using var outer = Capsule(new Rectangle(1, 1, Width - 3, Height - 3));
        using var selected = Capsule(active);
        e.Graphics.DrawPath(border, outer);
        e.Graphics.FillPath(background, selected); e.Graphics.DrawPath(border, selected);
        TextRenderer.DrawText(e.Graphics, "☼ Светлая", Font, new Rectangle(0, 0, half, Height), ForeColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        TextRenderer.DrawText(e.Graphics, "☾ Тёмная", Font, new Rectangle(half, 0, half, Height), ForeColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        if (Focused) ControlPaint.DrawFocusRectangle(e.Graphics, ClientRectangle);
    }
}
