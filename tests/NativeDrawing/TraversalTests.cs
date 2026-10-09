using KhitunGeo.Native;
using static GeometryTests;

static class TraversalTests
{
    private static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase) { "0", "A", "B", "C" };
    internal static DrawingScene Scene(params DrawingDefinition[] definitions) => new(definitions[0], definitions,
        All.Select(n => new DrawingLayer(n, true)), 0, 0);
    internal static DrawingDefinition Definition(int id, params DrawingNode[] nodes) => new(id, "B" + id, nodes,
        nodes.OfType<DrawingGeometryNode>().Aggregate(DrawingBounds.Empty, (b, g) => b.Union(g.Primitive.Bounds)));
    public static void Run()
    {
        var points = new[] { new SurveyPoint("1", 0, 0, null, ""), new SurveyPoint("2", 100, 100, null, "") };
        var fit = DrawingViewport.Fit(points, new DrawingBounds(-1e8, -1e8, 1e8, 1e8), 500, 500)!;
        Near(fit.World.MinE, -5, "Only points determine fit minE"); Near(fit.World.MinN, -5, "Point fit minN");
        Near(fit.World.MaxE, 105, "Point fit maxE"); Near(fit.World.MaxN, 105, "Point fit maxN");
        var single = DrawingViewport.Fit(points.Take(1).ToArray(), DrawingBounds.Empty, 500, 500)!;
        Near(single.World.MaxE - single.World.MinE, 1.1, "One point width");
        var overview = DrawingViewport.Fit(Array.Empty<SurveyPoint>(), new DrawingBounds(0, 0, 10, 20), 500, 500)!;
        Near(overview.World.MaxN, 21, "No points uses drawing");
        Check(DrawingViewport.Fit(new[] { new SurveyPoint("", double.NaN, 1, null, "") }, DrawingBounds.Empty, 500, 500) is null, "Invalid/absent points ignored");
        var geometry = Definition(1, new DrawingGeometryNode(new(DrawingKind.Line, "0", 0, 0, 10, 0)),
            new DrawingGeometryNode(new(DrawingKind.Point, "C", 5, 5)));
        var root = new DrawingDefinition(0, "model", new DrawingNode[] {
            new DrawingInsertNode(1, Affine2D.Identity, "A"), new DrawingInsertNode(1, new(1,0,0,1,100,100), "B") },
            new DrawingBounds(0,0,110,105));
        var scene = Scene(root, geometry); var result = new List<DrawingPrimitive>();
        var stats = DrawingTraversal.Visit(scene, All, new DrawingBounds(-10,-10,20,20), result.Add);
        Check(stats.Visited == 4 && stats.Emitted == 2, "Far insert skipped before children");
        Check(result[0].Layer == "A" && result[1].Layer == "C", "Zero inherited/explicit retained");
        result.Clear(); DrawingTraversal.Visit(scene, new HashSet<string> { "B", "C" }, null, result.Add);
        Check(result.Count == 2 && result[0].Layer == "B" && result[0].E1 == 100, "Same definition independent layer context");
        result.Clear(); DrawingTraversal.Visit(scene, new HashSet<string> { "C" }, null, result.Add);
        Check(result.Count == 0, "Hidden parent hides explicit children");
        var crossing = Scene(Definition(0, new DrawingGeometryNode(new(DrawingKind.Line, "0", -100, 50, 200, 50))));
        result.Clear(); DrawingTraversal.Visit(crossing, All, fit.World, result.Add);
        Check(result.Count == 1, "Crossing retained with both vertices outside");
        var nested = new DrawingDefinition(2, "nested", new[] { new DrawingInsertNode(1, Affine2D.Identity, "0") }, geometry.Bounds);
        var nestingRoot = new DrawingDefinition(0, "root", new[] { new DrawingInsertNode(2, Affine2D.Identity, "B") }, geometry.Bounds);
        result.Clear(); DrawingTraversal.Visit(Scene(nestingRoot, geometry, nested), All, null, result.Add);
        Check(result[0].Layer == "B", "Nested zero inheritance");
        var broadRoot = new DrawingDefinition(0, "root", Enumerable.Range(0, 100000).Select(_ => new DrawingInsertNode(1, Affine2D.Identity, "0")), geometry.Bounds);
        stats = DrawingTraversal.Visit(Scene(broadRoot, geometry), All, null, _ => { });
        Check(stats.Truncated && stats.Visited == 200000, "Frame traversal bounded despite shared geometry");
        var emissions = 0;
        stats = DrawingTraversal.Visit(Scene(broadRoot, geometry), All, null, _ => emissions++, () => emissions >= 2);
        Check(stats.Truncated && stats.Emitted == 2 && stats.Visited < 10, "Rendering budget stops traversal early");
        stats = DrawingTraversal.Visit(scene, new HashSet<string>(), null, _ => throw new Exception("Hidden emission"));
        Check(stats.Emitted == 0, "All hidden");
        var curve = DrawingGeometry.FromBulge("0", 0, 0, 10, 0, 1).Transform(new Affine2D(2, .5, 1, 3, 100, 200));
        var sampled = DrawingCurveSampler.Sample(curve, 50);
        Check(!sampled.DetailLimited && sampled.Points.Count > 2, "Normal curve detail");
        for (var i = 0; i < sampled.Points.Count - 1; i++)
        {
            var p = sampled.Points[i]; var q = sampled.Points[i + 1];
            var t = curve.StartAngle + curve.SweepRadians * (i + .5) / (sampled.Points.Count - 1); var mid = curve.At(t);
            var de = mid.E - (p.E + q.E) / 2; var dn = mid.N - (p.N + q.N) / 2;
            Check(Math.Sqrt(de * de + dn * dn) * 50 <= .75 + 1e-8, "Screen chord error");
        }
        var end = sampled.Points[^1]; var expected = curve.At(curve.StartAngle + curve.SweepRadians);
        Near(end.E, expected.E, "Sampler last endpoint"); Near(end.N, expected.N, "Sampler last endpoint N");
        sampled = DrawingCurveSampler.Sample(curve, 1e12);
        Check(sampled.DetailLimited && sampled.Points.Count == 1025, "Curve segments capped and reported");
        var renderStatus = new DrawingRenderStatus();
        renderStatus.Record(true, true, true); renderStatus.Record(false, false, false);
        Check(renderStatus.Truncated && renderStatus.DetailLimited, "Partial repaint preserves full-frame warnings");
        renderStatus.Record(true, false, false);
        Check(!renderStatus.Truncated && !renderStatus.DetailLimited, "Successful full paint clears prior warnings");
        renderStatus.Record(false, false, true);
        Check(renderStatus.DetailLimited && !renderStatus.Truncated, "Partial repaint merges new warning");
        Console.WriteLine("PASS point-only viewport, layer inheritance, culling and bounded curve/traversal");
    }
}
