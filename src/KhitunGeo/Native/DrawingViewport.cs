namespace KhitunGeo.Native;

internal sealed record DrawingViewport(DrawingBounds World, double Scale, double CenterE, double CenterN, int Width, int Height)
{
    public static DrawingViewport? Fit(IReadOnlyList<SurveyPoint> points, DrawingBounds visibleDrawingBounds, int width, int height)
    {
        if (width <= 48 || height <= 48) return null;
        var b = DrawingBounds.Empty;
        foreach (var p in points)
            if (p.X is double n && p.Y is double e && double.IsFinite(e) && double.IsFinite(n)) b = b.Include(e, n);
        b = b.Union(visibleDrawingBounds);
        if (!b.IsFinite) return null;
        var extentE = b.MaxE - b.MinE; var extentN = b.MaxN - b.MinN;
        if (extentE == 0) extentE = 1; if (extentN == 0) extentN = 1;
        var centerE = b.MinE / 2 + b.MaxE / 2; var centerN = b.MinN / 2 + b.MaxN / 2;
        var world = new DrawingBounds(centerE - extentE * .55, centerN - extentN * .55, centerE + extentE * .55, centerN + extentN * .55);
        var scale = Math.Min((width - 48) / (extentE * 1.1), (height - 48) / (extentN * 1.1));
        return world.IsFinite && double.IsFinite(scale) && scale > 0 ? new(world, scale, centerE, centerN, width, height) : null;
    }
    public (double X, double Y) Screen(double e, double n) => (Width / 2d + (e - CenterE) * Scale, Height / 2d - (n - CenterN) * Scale);
    public (double E, double N) WorldAt(double x, double y)
        => (CenterE + (x - Width / 2d) / Scale, CenterN - (y - Height / 2d) / Scale);
    public DrawingViewport ZoomAt(double x, double y, double factor)
    {
        if (!double.IsFinite(factor) || factor <= 0) return this;
        var scale = Math.Clamp(Scale * factor, 1e-12, 1e12);
        var anchor = WorldAt(x, y);
        var centerE = anchor.E - (x - Width / 2d) / scale;
        var centerN = anchor.N + (y - Height / 2d) / scale;
        return this with { Scale = scale, CenterE = centerE, CenterN = centerN };
    }
    public DrawingViewport PanPixels(double dx, double dy)
        => this with { CenterE = CenterE - dx / Scale, CenterN = CenterN + dy / Scale };
    public DrawingViewport Resize(int width, int height) => this with { Width = width, Height = height };
    public DrawingBounds Clip(double left, double top, double right, double bottom)
        => new(CenterE + (left - Width / 2d) / Scale, CenterN - (bottom - Height / 2d) / Scale,
            CenterE + (right - Width / 2d) / Scale, CenterN - (top - Height / 2d) / Scale);
}
