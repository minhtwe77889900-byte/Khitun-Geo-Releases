using System.Drawing.Drawing2D;
using System.Runtime.CompilerServices;

namespace KhitunGeo.Native;

internal static class BrandArtwork
{
    private static readonly ConditionalWeakTable<Control, ArtworkCache> cache = new();
    private static readonly PointF[][] contours = BuildContours();
    public static void PaintBackdrop(Control owner, Graphics graphics)
    {
        Control? backdrop = owner.Parent;
        while (backdrop is not null && backdrop is not BrandPanel and not BrandLayoutPanel and not BrandNavigationPanel && backdrop.BackColor.A < 255) backdrop = backdrop.Parent;
        using var fill = new SolidBrush(backdrop?.BackColor ?? SystemColors.Control);
        graphics.FillRectangle(fill, owner.ClientRectangle);
        if (backdrop is not BrandPanel and not BrandLayoutPanel and not BrandNavigationPanel) return;
        var position = backdrop.PointToClient(owner.PointToScreen(Point.Empty));
        var state = graphics.Save();
        graphics.TranslateTransform(-position.X, -position.Y);
        Paint(backdrop, graphics);
        graphics.Restore(state);
    }
    public static void Paint(Control owner, Graphics graphics)
    {
        if (owner.ClientSize.Height <= 0) return;
        var cached = cache.GetValue(owner, control =>
        {
            var result = new ArtworkCache();
            control.Disposed += (_, _) => result.Dispose();
            return result;
        });
        var dark = NativeTheme.IsDark(owner);
        if (cached.Image is null || cached.Image.Height != owner.ClientSize.Height || cached.Dark != dark)
        {
            cached.Dispose(); cached.Dark = dark;
            cached.Image = new Bitmap(680, owner.ClientSize.Height);
            using var buffer = Graphics.FromImage(cached.Image);
            Render(buffer, 680, owner.ClientSize.Height, dark);
        }
        graphics.DrawImageUnscaled(cached.Image, Point.Empty);
    }
    private static void Render(Graphics graphics, int width, int height, bool dark)
    {
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var plane = new SolidBrush(Color.FromArgb(dark ? 35 : 18, 8, 145, 155));
        graphics.FillPolygon(plane, new[] { new Point(0, height * 2 / 3), new Point(Math.Min(width, 240), height), new Point(0, height) });
        graphics.FillPolygon(plane, new[] { new Point(Math.Min(width, 350), 0), new Point(Math.Min(width, 500), 0), new Point(Math.Min(width, 330), 210), new Point(Math.Min(width, 190), 210) });
        graphics.FillPolygon(plane, new[] { new Point(Math.Min(width, 520), 0), new Point(Math.Min(width, 680), 0), new Point(Math.Min(width, 600), 115) });
        // Quiet topographic decoration is generated from fixed contours, with no I/O.
        using var contour = new Pen(Color.FromArgb(dark ? 36 : 20, dark ? 37 : 8, dark ? 174 : 132, dark ? 179 : 141), .7f);
        var state = graphics.Save();
        graphics.SetClip(new Rectangle(0, 0, Math.Min(width, 620), height), CombineMode.Intersect);
        foreach (var points in contours) graphics.DrawLines(contour, points);
        graphics.Restore(state);
    }
    private static PointF[][] BuildContours()
    {
        var contours = new List<PointF[]>();
        foreach (var center in new[] { new PointF(65, 75), new PointF(160, 355), new PointF(-30, 640) })
            for (var level = 0; level < 32; level++)
            {
                var points = new PointF[97];
                for (var i = 0; i < points.Length; i++)
                {
                    var angle = i * Math.PI * 2 / 96;
                    var radius = 14 + level * 6 + 6 * Math.Sin(angle * 5 + level * .08) + 4 * Math.Cos(angle * 9);
                    points[i] = new(center.X + (float)(radius * Math.Cos(angle)), center.Y + (float)(radius * .69 * Math.Sin(angle)));
                }
                contours.Add(points);
            }
        return contours.ToArray();
    }
    private sealed class ArtworkCache : IDisposable
    {
        public Bitmap? Image;
        public bool Dark;
        public void Dispose() { Image?.Dispose(); Image = null; }
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
