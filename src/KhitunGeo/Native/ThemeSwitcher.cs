using System.Drawing.Drawing2D;

namespace KhitunGeo.Native;

internal sealed class ThemeSwitcher : Control
{
    public bool Dark { get; private set; }
    private bool paintedDark;
    public event EventHandler? ThemeChanged;
    public ThemeSwitcher()
    {
        Size = new Size(160, 40); Cursor = Cursors.Hand; AccessibleName = "Светлая или тёмная тема";
        SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        BackColor = Color.Transparent;
        TabStop = true;
    }
    public void SetDark(bool value)
    {
        if (Dark == value) return;
        Dark = value; ThemeChanged?.Invoke(this, EventArgs.Empty); CommitVisualState();
    }
    public void CommitVisualState() { paintedDark = Dark; Invalidate(); }
    protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); Focus(); SetDark(Height > Width ? e.Y >= Height / 2 : e.X >= Width / 2); }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode is Keys.Space or Keys.Left or Keys.Right or Keys.Up or Keys.Down) { SetDark(e.KeyCode is Keys.Left or Keys.Up ? false : e.KeyCode is Keys.Right or Keys.Down ? true : !Dark); e.Handled = true; }
    }
    private static GraphicsPath Capsule(Rectangle bounds)
    {
        var path = new GraphicsPath(); var diameter = Math.Min(bounds.Height, bounds.Width);
        if (bounds.Height > bounds.Width)
        {
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 180);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 0, 180);
        }
        else
        {
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 90, 180);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 180);
        }
        path.CloseFigure(); return path;
    }
    protected override void OnPaintBackground(PaintEventArgs e) => BrandArtwork.PaintBackdrop(this, e.Graphics);
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var border = new Pen(paintedDark ? Color.FromArgb(54, 185, 174) : Color.FromArgb(8, 127, 115));
        using var background = new SolidBrush(paintedDark ? Color.FromArgb(24, 53, 59) : Color.FromArgb(226, 241, 235));
        bool vertical = Height > Width;
        var first = vertical ? new Rectangle(2, 2, Width - 4, Height / 2 - 2) : new Rectangle(2, 2, Width / 2 - 2, Height - 4);
        var second = vertical ? new Rectangle(2, Height / 2, Width - 4, Height / 2 - 2) : new Rectangle(Width / 2, 2, Width / 2 - 2, Height - 4);
        using var outer = Capsule(new Rectangle(1, 1, Width - 3, Height - 3));
        using var selected = Capsule(paintedDark ? second : first);
        e.Graphics.DrawPath(border, outer);
        e.Graphics.FillPath(background, selected); e.Graphics.DrawPath(border, selected);
        using var iconFont = new Font("Segoe MDL2 Assets", 20);
        const TextFormatFlags flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.PreserveGraphicsClipping | TextFormatFlags.PreserveGraphicsTranslateTransform;
        TextRenderer.DrawText(e.Graphics, "\uE706", iconFont, first, ForeColor, flags);
        TextRenderer.DrawText(e.Graphics, "\uE708", iconFont, second, ForeColor, flags);
        if (Focused) ControlPaint.DrawFocusRectangle(e.Graphics, ClientRectangle);
    }
}
