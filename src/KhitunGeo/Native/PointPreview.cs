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
    private DrawingViewport? viewport;
    private double zoomFactor = 1;
    private Point? panStart;
    public bool Truncated => renderStatus.Truncated;
    public bool DetailLimited => renderStatus.DetailLimited;
    public int ZoomPercent => (int)Math.Round(zoomFactor * 100);
    public event EventHandler? DrawingStatusChanged;
    public event EventHandler? ZoomChanged;

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
        overviewDirty = true; overview = DrawingBounds.Empty; overviewTruncated = false; viewport = null; zoomFactor = 1;
        SetStatus(false, false);
        FitToContent();
    }

    public PointPreview()
    {
        Dock = DockStyle.Fill;
        DoubleBuffered = true;
        ResizeRedraw = true;
        SetStyle(ControlStyles.Selectable, true);
        TabStop = true;
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

    public void FitToContent()
    {
        if (!active || !Visible) { viewport = null; return; }
        if (Width <= 48 || Height <= 48) { viewport = null; return; }
        UpdateDrawingOverview();
        viewport = DrawingViewport.Fit(points, overview, Width, Height);
        zoomFactor = 1;
        ZoomChanged?.Invoke(this, EventArgs.Empty);
        if (active && Visible) Invalidate();
    }

    public void ZoomBy(double factor)
    {
        if (viewport is null) return;
        var nextZoom = Math.Clamp(zoomFactor * factor, 0.01, 1000);
        var actualFactor = nextZoom / zoomFactor;
        viewport = viewport.ZoomAt(Width / 2d, Height / 2d, actualFactor);
        zoomFactor = nextZoom;
        ZoomChanged?.Invoke(this, EventArgs.Empty);
        Invalidate();
    }

    private void UpdateDrawingOverview()
    {
        if (!overviewDirty) return;
        overview = DrawingBounds.Empty; overviewTruncated = false;
        if (drawing is not null)
            overviewTruncated = DrawingTraversal.Visit(drawing, visibleLayers, null, p => overview = overview.Union(p.Bounds)).Truncated;
        overviewDirty = false;
    }

    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); Focus(); }
    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        if (viewport is null) return;
        var factor = Math.Pow(1.2, e.Delta / 120d);
        var nextZoom = Math.Clamp(zoomFactor * factor, 0.01, 1000);
        viewport = viewport.ZoomAt(e.X, e.Y, nextZoom / zoomFactor);
        zoomFactor = nextZoom;
        ZoomChanged?.Invoke(this, EventArgs.Empty);
        Invalidate();
    }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        if (e.Button != MouseButtons.Middle || viewport is null) return;
        panStart = e.Location; Capture = true; Cursor = Cursors.Hand;
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (panStart is not Point start || viewport is null) return;
        viewport = viewport.PanPixels(e.X - start.X, e.Y - start.Y);
        panStart = e.Location;
        Invalidate();
    }
    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Middle) return;
        panStart = null; Capture = false; Cursor = Cursors.Default;
    }
    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        base.OnMouseDoubleClick(e);
        FitToContent();
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
        if (viewport is null)
        {
            UpdateDrawingOverview();
            viewport = DrawingViewport.Fit(points, overview, Width, Height);
        }
        if (viewport is null) { SetStatus(!hasPoints && overviewTruncated, false, e.ClipRectangle.Contains(ClientRectangle)); return; }
        viewport = viewport.Resize(Width, Height);
        var activeViewport = viewport;
        var region = new RectangleF(0, 0, Width - 1, Height - 1);
        var clip = activeViewport.Clip(e.ClipRectangle.Left, e.ClipRectangle.Top, e.ClipRectangle.Right, e.ClipRectangle.Bottom);
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
                var start = activeViewport.Screen(line.E1, line.N1); var end = activeViewport.Screen(line.E2, line.N2);
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
                        var screen = activeViewport.Screen(p.E1, p.N1);
                        e.Graphics.DrawEllipse(pen, (float)screen.X - 2, (float)screen.Y - 2, 4, 4);
                    }
                    else
                    {
                        var samples = DrawingCurveSampler.Sample(p, activeViewport.Scale); detailLimited |= samples.DetailLimited;
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
                var screen = activeViewport.Screen(east, n);
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
