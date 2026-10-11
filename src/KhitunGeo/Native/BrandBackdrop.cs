using System.Drawing.Drawing2D;

namespace KhitunGeo.Native;

internal static class BrandArtwork
{
    public static void Paint(Control owner, Graphics graphics)
    {
        var dark = NativeTheme.IsDark(owner);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var plane = new SolidBrush(Color.FromArgb(dark ? 18 : 15, 8, 127, 115));
        var width = owner.ClientSize.Width; var height = owner.ClientSize.Height;
        graphics.FillPolygon(plane, new[] { new Point(0, 0), new Point(Math.Min(width, 240), 0), new Point(0, Math.Min(height, 240)) });
        graphics.FillPolygon(plane, new[] { new Point(0, height), new Point(Math.Min(width, 200), height), new Point(0, Math.Max(0, height - 270)) });
        using var contour = new Pen(Color.FromArgb(dark ? 32 : 25, dark ? 70 : 8, dark ? 180 : 127, dark ? 170 : 115), 1);
        // Decorative top-down contour lines; unrelated to the coordinate engine.
        for (var level = 0; level < 16; level++)
        {
            var points = new PointF[65];
            for (var i = 0; i < points.Length; i++)
            {
                var x = i * Math.Min(width, 640) / 64f;
                var y = level * 12 + 14 * Math.Sin(x / 49 + level * .19) + 9 * Math.Sin(x / 23 + level * .13);
                points[i] = new(x, (float)y);
            }
            graphics.DrawLines(contour, points);
        }
    }
}

internal sealed class BrandPanel : Panel
{
    public BrandPanel() { DoubleBuffered = true; ResizeRedraw = true; }
    protected override void OnPaintBackground(PaintEventArgs e) { base.OnPaintBackground(e); BrandArtwork.Paint(this, e.Graphics); }
}

internal sealed class BrandLayoutPanel : TableLayoutPanel
{
    public BrandLayoutPanel() { DoubleBuffered = true; ResizeRedraw = true; }
    protected override void OnPaintBackground(PaintEventArgs e) { base.OnPaintBackground(e); BrandArtwork.Paint(this, e.Graphics); }
}

internal sealed class BrandNavigationPanel : FlowLayoutPanel
{
    public BrandNavigationPanel() { DoubleBuffered = true; ResizeRedraw = true; }
    protected override void OnPaintBackground(PaintEventArgs e) { base.OnPaintBackground(e); BrandArtwork.Paint(this, e.Graphics); }
}

internal sealed class WorkspaceSplitContainer : SplitContainer
{
    public WorkspaceSplitContainer()
    {
        DoubleBuffered = true; ResizeRedraw = true;
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
        SplitterWidth = 8;
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (Panel2Collapsed || Panel1Collapsed) return;
        var area = SplitterRectangle;
        using var pen = new Pen(NativeTheme.IsDark(this) ? Color.FromArgb(62, 89, 98) : Color.FromArgb(198, 218, 216));
        e.Graphics.DrawLine(pen, area.Left + area.Width / 2, area.Top + 12, area.Left + area.Width / 2, area.Bottom - 12);
    }
}
