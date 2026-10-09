namespace KhitunGeo.Native;

internal sealed class PassportState
{
    public ConversionPassport? Current { get; internal set; }
}

// Validate the complete passport before committing the real workspace.
internal sealed class PassportConversion
{
    private readonly SurveyPoint[] points;
    private readonly CoordinateConversion conversion;
    private readonly PassportCapture capture;
    public string Request => conversion.Request;

    public PassportConversion(IReadOnlyList<SurveyPoint> points, string settings, string sourceName,
        string targetName, string appVersion, DateTimeOffset startedAt)
    {
        this.points = points.ToArray();
        conversion = new CoordinateConversion(this.points, settings);
        capture = PassportCapture.Capture(this.points, sourceName, targetName, settings, appVersion, startedAt);
    }

    public IReadOnlyList<int> Apply(PointWorkspace workspace, string currentSettings, string response,
        IReadOnlyList<QualityIssue> issues, PassportState state, Action? restoreSettings = null)
    {
        var candidate = new PointWorkspace(); candidate.ReplacePoints(points);
        var zones = conversion.Apply(candidate, currentSettings, response);
        var completed = capture.Complete(candidate.Points, zones, issues);
        var previous = state.Current;
        conversion.Apply(workspace, currentSettings, response, () =>
        {
            restoreSettings?.Invoke();
            state.Current = previous;
        }, recordUnchanged: true);
        state.Current = completed;
        return zones;
    }
}
