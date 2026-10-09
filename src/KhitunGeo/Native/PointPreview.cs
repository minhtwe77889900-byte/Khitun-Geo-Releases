namespace KhitunGeo.Native;

internal sealed class PointPreview : Control
{
    private IReadOnlyList<SurveyPoint> points = Array.Empty<SurveyPoint>();
    private bool active = true;
    private DrawingScene? drawing;
    private IReadOnlySet<string> visibleLayers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private DrawingBounds overview = DrawingBounds.Empty;
    private bool overviewDirty = true, overviewTruncated;
    private readonly DrawingRenderStatus renderStatus = new();
    private readonly System.Windows.Forms.Timer pointRefreshTimer = new() { Interval = 120 };
    public bool Truncated => renderStatus.Truncated;
    public bool DetailLimited => renderStatus.DetailLimited;
    public event EventHandler? DrawingStatusChanged;

    public void SetVisibleLayers(IReadOnlySet<string> layers)
    {
        visibleLayers = new HashSet<string>(layers, StringComparer.OrdinalIgnoreCase);
        overviewDirty = true;
        if (active && Visible) Invalidate();
    }

    public void SetDrawing(DrawingScene? value)
    {
        drawing = value;
        visibleLayers = new HashSet<string>(value?.Layers.Where(l => l.InitiallyVisible).Select(l => l.Name) ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        overviewDirty = true; overview = DrawingBounds.Empty; overviewTruncated = false;
        SetStatus(false, false);
        if (active && Visible) Invalidate();
    }

    public PointPreview()
    {
        Dock = DockStyle.Fill;
        DoubleBuffered = true;
        ResizeRedraw = true;
        BackColor = Color.White;
        pointRefreshTimer.Tick += (_, _) =>
        {
            pointRefreshTimer.Stop();
            if (active && Visible) Invalidate();
        };
    }

    public void SetPoints(IReadOnlyList<SurveyPoint> values)
    {
        points = values;
        pointRefreshTimer.Stop();
        if (active && Visible) pointRefreshTimer.Start();
    }

    public void SetActive(bool value)
    {
        active = value;
        Visible = value;
        pointRefreshTimer.Stop();
        if (value) Invalidate();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) pointRefreshTimer.Dispose();
        base.Dispose(disposing);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (!active || !Visible) return;
        base.OnPaint(e);
        if (Width <= 48 || Height <= 48) return;
        var hasPoints = points.Any(p => p.X is double n && p.Y is double east && double.IsFinite(n) && double.IsFinite(east));
        if (!hasPoints && overviewDirty)
        {
            overview = DrawingBounds.Empty; overviewTruncated = false;
            if (drawing is not null)
                overviewTruncated = DrawingTraversal.Visit(drawing, visibleLayers, null, p => overview = overview.Union(p.Bounds)).Truncated;
            overviewDirty = false;
        }
        var viewport = DrawingViewport.Fit(points, hasPoints ? DrawingBounds.Empty : overview, Width, Height);
        if (viewport is null) { SetStatus(!hasPoints && overviewTruncated, false, e.ClipRectangle.Contains(ClientRectangle)); return; }
        var world = viewport.World;
        var a = viewport.Screen(world.MinE, world.MaxN); var z = viewport.Screen(world.MaxE, world.MinN);
        var region = RectangleF.FromLTRB((float)a.X, (float)a.Y, (float)z.X, (float)z.Y);
        var clip = viewport.Clip(e.ClipRectangle.Left, e.ClipRectangle.Top, e.ClipRectangle.Right, e.ClipRectangle.Bottom);
        using var brush = new SolidBrush(Color.FromArgb(15, 157, 145));
        using var pen = new Pen(ForeColor);
        e.Graphics.DrawRectangle(pen, region.X, region.Y, region.Width, region.Height);
        var saved = e.Graphics.Save(); var truncated = !hasPoints && overviewTruncated; var detailLimited = false;
        var drawingWork = 0;
        try
        {
            e.Graphics.SetClip(region, System.Drawing.Drawing2D.CombineMode.Intersect);
            void Line(double e1, double n1, double e2, double n2)
            {
                if (drawingWork >= 200000) { truncated = true; return; }
                drawingWork++;
                var clipped = DrawingGeometry.ClipLine(e1, n1, e2, n2, clip);
                if (clipped is not { } line) return;
                var start = viewport.Screen(line.E1, line.N1); var end = viewport.Screen(line.E2, line.N2);
                e.Graphics.DrawLine(pen, (float)start.X, (float)start.Y, (float)end.X, (float)end.Y);
            }
            if (drawing is not null)
            {
                var traversal = DrawingTraversal.Visit(drawing, visibleLayers, clip, p =>
                {
                    if (p.Kind == DrawingKind.Line) Line(p.E1, p.N1, p.E2, p.N2);
                    else if (p.Kind == DrawingKind.Point)
                    {
                        drawingWork++;
                        var screen = viewport.Screen(p.E1, p.N1);
                        e.Graphics.DrawEllipse(pen, (float)screen.X - 2, (float)screen.Y - 2, 4, 4);
                    }
                    else
                    {
                        var samples = DrawingCurveSampler.Sample(p, viewport.Scale); detailLimited |= samples.DetailLimited;
                        for (var i = 1; i < samples.Points.Count; i++)
                        {
                            if (drawingWork >= 200000) { truncated = true; break; }
                            var start = samples.Points[i - 1]; var end = samples.Points[i]; Line(start.E, start.N, end.E, end.N);
                        }
                    }
                }, () => drawingWork >= 200000).Truncated;
                truncated = truncated || traversal;
            }
            foreach (var p in points)
            {
                if (p.X is not double n || p.Y is not double east || !double.IsFinite(n) || !double.IsFinite(east)) continue;
                var screen = viewport.Screen(east, n);
                if (screen.X >= e.ClipRectangle.Left - 3 && screen.X <= e.ClipRectangle.Right + 3 && screen.Y >= e.ClipRectangle.Top - 3 && screen.Y <= e.ClipRectangle.Bottom + 3)
                    e.Graphics.FillEllipse(brush, (float)screen.X - 3, (float)screen.Y - 3, 6, 6);
            }
        }
        finally { e.Graphics.Restore(saved); }
        SetStatus(truncated, detailLimited, e.ClipRectangle.Contains(ClientRectangle));
    }
    private void SetStatus(bool truncated, bool detailLimited, bool fullPaint = true)
    {
        if (renderStatus.Record(fullPaint, truncated, detailLimited)) DrawingStatusChanged?.Invoke(this, EventArgs.Empty);
    }
}
