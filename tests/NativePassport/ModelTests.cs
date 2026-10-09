using System.Globalization;
using KhitunGeo.Native;

internal static class ModelTests
{
    internal static void Assert(bool ok, string message) { if (!ok) throw new Exception(message); }
    internal static void Reject(Action action)
    { try { action(); } catch (ArgumentException) { return; } throw new Exception("Expected validation failure"); }
    internal static void Run()
    {
        var points = new[] { new SurveyPoint("001",65,84,-12,"Глубина"), new SurveyPoint("002",65.01,84.02,null,"") };
        foreach (var culture in new[] { "ru-RU", "en-US" })
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            Assert(ConversionPassport.Fingerprint(points) == "479F642610254F147B10973F560256ED0C6F658AC67EEB853D6C01CDBE762DD6", "FingerprintGolden " + culture);
        }
        var hash = ConversionPassport.Fingerprint(points);
        foreach (var changed in new[] { points[0] with { Name = "1" }, points[0] with { X = 66 }, points[0] with { Y = 85 }, points[0] with { Height = null }, points[0] with { Description = "other" } })
            Assert(ConversionPassport.Fingerprint(new[] { changed, points[1] }) != hash, "FingerprintChangesEachField");
        Assert(ConversionPassport.Fingerprint(points.Reverse().ToArray()) != hash, "FingerprintChangesRowOrder");
        const string settings = "{\"version\":1,\"source\":{\"kind\":\"geo\",\"datum\":\"wgs\"},\"target\":{\"kind\":\"fixedtm\",\"datum\":\"sk42\"},\"zone\":{\"auto\":true}}";
        var date = new DateTimeOffset(2026,10,8,21,0,0,TimeSpan.FromHours(7));
        var capture = PassportCapture.Capture(points, "WGS-84", "МСК-164", settings, "1.7.5", date);
        var result = points.Select(p => p with { X = p.X + 100, Y = p.Y + 200 }).ToArray();
        var before = points.ToArray(); points[0] = points[0] with { Name = "changed" };
        var issues = Enumerable.Range(0,700).Select(i => new QualityIssue(i % 2, "warning", "Нет высоты " + i)).ToArray();
        var zones = new[] { 15 };
        var passport = capture.Complete(result, zones, issues);
        Assert(passport.StartedAt.Offset == TimeSpan.Zero && passport.StartedAt.Hour == 14, "UTC date");
        Assert(passport.SourceName == "WGS-84" && passport.TargetName == "МСК-164" && passport.Settings == settings && passport.AppVersion == "1.7.5", "Captured descriptors");
        Assert(passport.Count == 2 && passport.BeforeHash == hash && passport.AfterHash == ConversionPassport.Fingerprint(result), "CompletionPreservesCountAndHashes");
        Assert(passport.Matches(result) && !passport.Matches(before), "MatchesAfterEdit");
        Assert(result[0].Height == -12 && result[1].Height is null, "NullAndNegativeHeights");
        Assert(passport.IssueCount == 700 && passport.Issues.Count == 500 && passport.Issues[499].Message == "Нет высоты 499", "500 issue limit");
        issues[0] = new(0, "error", "changed"); zones[0] = 20;
        Assert(passport.Issues[0].Message == "Нет высоты 0" && passport.Zones[0] == 15, "Owned issue and zone snapshots");
        Reject(() => capture.Complete(new[] { result[0] }, new[] { 15 }, Array.Empty<QualityIssue>()));
        Reject(() => capture.Complete(new[] { result[0] with { Height = 0 }, result[1] }, new[] { 15 }, Array.Empty<QualityIssue>()));
        Reject(() => capture.Complete(new[] { result[0] with { Name = "bad" }, result[1] }, new[] { 15 }, Array.Empty<QualityIssue>()));
        Reject(() => capture.Complete(result, new[] { 0 }, Array.Empty<QualityIssue>()));
        Reject(() => capture.Complete(result, new[] { 15 }, new[] { new QualityIssue(0, "error", "bad") }));
        Reject(() => capture.Complete(result, new[] { 15 }, new[] { new QualityIssue(2, "warning", "bad") }));
        Reject(() => ConversionPassport.Fingerprint(new[] { result[0] with { X = double.NaN } }));
        var closing = new DeferredCloseRequest(); var closeCalls = 0;
        closing.Complete(() => closeCalls++);
        Assert(closeCalls == 0, "Completed operation must not close a window without a request");
        closing.Request(); closing.Request(); closing.Complete(() => closeCalls++);
        Assert(closeCalls == 1, "Repeated close requests must replay normal closing once");
        closing.Complete(() => closeCalls++);
        Assert(closeCalls == 1, "Close request must be consumed before replay");
        closing.Request(); closing.Complete(() => closeCalls++);
        Assert(closeCalls == 2, "A later close request after cancelled save prompt must work");
        Console.WriteLine("PASS passport model");
    }
}
