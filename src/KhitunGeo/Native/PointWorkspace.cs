namespace KhitunGeo.Native;

internal sealed class PointWorkspace
{
    private SurveyPoint[] points = Array.Empty<SurveyPoint>();
    private readonly List<(SurveyPoint[] Points, Action? OnUndo)> history = new();
    public IReadOnlyList<SurveyPoint> Points => Array.AsReadOnly(points);
    public event EventHandler? Changed;

    public void ReplacePoints(IReadOnlyList<SurveyPoint> values, Action? onUndo = null, bool recordUnchanged = false)
    {
        var next = values.ToArray();
        foreach (var point in next) ValidatePoint(point);
        if (!recordUnchanged && points.SequenceEqual(next)) return;
        history.Add((points, onUndo));
        if (history.Count > 100) history.RemoveAt(0);
        points = next;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void SetPoint(int index, SurveyPoint point)
    {
        var next = (SurveyPoint[])points.Clone();
        next[index] = point;
        ReplacePoints(next);
    }

    public void OffsetHeight(double offset)
    {
        if (!double.IsFinite(offset)) throw new ArgumentException("Invalid height offset.");
        ReplacePoints(points.Select(p => p with { Height = p.Height + offset }).ToArray());
    }

    public bool Undo()
    {
        if (history.Count == 0) return false;
        var entry = history[^1];
        entry.OnUndo?.Invoke();
        points = entry.Points;
        history.RemoveAt(history.Count - 1);
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public void SwapXY() => ReplacePoints(points.Select(p => p with { X = p.Y, Y = p.X }).ToArray());

    internal static void ValidatePoint(SurveyPoint point)
    {
        if (new[] { point.X, point.Y, point.Height }.Any(v => v.HasValue && !double.IsFinite(v.Value)))
            throw new ArgumentException("Coordinates must be finite.");
    }
}
