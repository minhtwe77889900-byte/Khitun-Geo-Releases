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
        BackColor = SystemColors.Control;
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
    private void DrawIcon(Graphics graphics, Rectangle bounds, bool moon)
    {
        var scale = DeviceDpi / 96f;
        var radius = Math.Min(10 * scale, Math.Min(bounds.Width, bounds.Height) * .28f);
        var cx = bounds.Left + bounds.Width / 2f; var cy = bounds.Top + bounds.Height / 2f;
        using var pen = new Pen(ForeColor, Math.Max(1.5f, 1.7f * scale)) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        if (moon)
        {
            // Both icons share exactly the same horizontal anchor; no glyph bearings.
            graphics.DrawEllipse(pen, cx - radius, cy - radius, radius * 2, radius * 2);
            graphics.DrawArc(pen, cx - radius * .25f, cy - radius, radius * 1.25f, radius * 2, 90, 180);
        }
        else
        {
            var disk = radius * .53f;
            graphics.DrawEllipse(pen, cx - disk, cy - disk, disk * 2, disk * 2);
            for (int i = 0; i < 8; i++)
            {
                var angle = i * Math.PI / 4;
                graphics.DrawLine(pen, cx + (float)Math.Cos(angle) * radius * .78f, cy + (float)Math.Sin(angle) * radius * .78f,
                    cx + (float)Math.Cos(angle) * radius, cy + (float)Math.Sin(angle) * radius);
            }
        }
    }
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
        DrawIcon(e.Graphics, first, false);
        DrawIcon(e.Graphics, second, true);
        if (Focused) ControlPaint.DrawFocusRectangle(e.Graphics, ClientRectangle);
    }
}
