using KhitunGeo.Native;

static class GeometryTests
{
    internal static void Check(bool ok, string name) { if (!ok) throw new Exception(name); }
    internal static void Near(double actual, double expected, string name, double tolerance = 1e-8)
        => Check(Math.Abs(actual - expected) <= tolerance, $"{name}: {actual} != {expected}");
    public static void Run()
    {
        foreach (var bulge in new[] { 1d, -1d, 2d })
        {
            var p = DrawingGeometry.FromBulge("0", 0, 0, 10, 0, bulge);
            var a = p.At(p.StartAngle); var z = p.At(p.StartAngle + p.SweepRadians);
            Near(a.E, 0, "Bulge start E"); Near(a.N, 0, "Bulge start N");
            Near(z.E, 10, "Bulge end E"); Near(z.N, 0, "Bulge end N");
            if (Math.Abs(bulge) == 1)
            {
                var mid = p.At(p.StartAngle + p.SweepRadians / 2);
                Near(mid.E, 5, "Semicircle middle E"); Near(mid.N, -5 * bulge, "Bulge direction");
                Near(p.Bounds.MinN, bulge > 0 ? -5 : 0, "Tight arc lower bound");
                Near(p.Bounds.MaxN, bulge > 0 ? 0 : 5, "Tight arc upper bound");
            }
            else Check(p.SweepRadians > Math.PI, "Major arc retained");
        }
        Check(DrawingGeometry.FromBulge("0", 0, 0, 10, 0, 0).Kind == DrawingKind.Line, "Zero bulge line");
        Check(DrawingGeometry.FromBulge("0", 0, 0, 0, 0, 1).Kind == DrawingKind.Point, "Coincident vertices");
        var arc = new DrawingPrimitive(DrawingKind.Arc, "0", 0, 0, Radius: 5, StartAngle: 0, EndAngle: Math.PI / 2);
        Near(arc.Bounds.MinE, 0, "Quadrant bound E"); Near(arc.Bounds.MinN, 0, "Quadrant bound N");
        var circle = new DrawingPrimitive(DrawingKind.Circle, "0", 0, 0, Radius: 1);
        var ellipse = circle.Transform(new Affine2D(2, 0, 0, 3, 10, 20));
        Near(ellipse.Bounds.MinE, 8, "Ellipse west"); Near(ellipse.Bounds.MaxN, 23, "Ellipse north");
        var reflected = arc.Transform(new Affine2D(-1, 0, 0, 1, 0, 0));
        Near(reflected.At(Math.PI / 4).E, -5 / Math.Sqrt(2), "Reflected E");
        Near(reflected.At(Math.PI / 4).N, 5 / Math.Sqrt(2), "Reflected N");
        var parent = new Affine2D(0, -1, 1, 0, 100, 200);
        var child = new Affine2D(2, 0, 0, 3, -20, -60);
        var transformed = parent.Compose(child).Apply(11, 22);
        Near(transformed.E, 94, "Base/rotation composition E"); Near(transformed.N, 202, "Base/rotation composition N");
        var source = new List<DrawingNode> { new DrawingGeometryNode(circle) };
        var definition = new DrawingDefinition(0, "model", source, circle.Bounds);
        source.Clear(); Check(definition.Nodes.Count == 1, "Definition owns nodes");
        var clipped = DrawingGeometry.ClipLine(-100, 50, 200, 50, new DrawingBounds(0, 0, 100, 100))!.Value;
        Near(clipped.E1, 0, "Clipped start"); Near(clipped.E2, 100, "Clipped end");
        Check(DrawingGeometry.ClipLine(-100, -100, -50, -50, new DrawingBounds(0, 0, 100, 100)) is null, "Offscreen line discarded");
        clipped = DrawingGeometry.ClipLine(-1e308, 50, 1e308, 50, new DrawingBounds(0, 0, 100, 100))!.Value;
        Check(double.IsFinite(clipped.E1) && double.IsFinite(clipped.E2), "Extreme crossing clipped before float conversion");
        foreach (var bulge in new[] { 1e-9, -1e-12, 1e-12, 1e-15, -1e-16 })
        {
            var shallow = DrawingGeometry.FromBulge("0", 450000, 7000000, 460000, 7010000, bulge).Transform(Affine2D.Identity);
            var start = shallow.At(shallow.StartAngle); var end = shallow.At(shallow.StartAngle + shallow.SweepRadians);
            Near(start.E, 450000, "Shallow start E", 1e-6); Near(start.N, 7000000, "Shallow start N", 1e-6);
            Near(end.E, 460000, "Shallow end E", 1e-6); Near(end.N, 7010000, "Shallow end N", 1e-6);
            Check(shallow.Bounds.MinE <= 450000 + 1e-6 && shallow.Bounds.MaxE >= 460000 - 1e-6, "Shallow stable bounds");
            var midpoint = shallow.At(shallow.StartAngle + shallow.SweepRadians / 2);
            Near(midpoint.E, 455000 + 5000 * bulge, "Shallow exact midpoint E", 1e-6);
            Near(midpoint.N, 7005000 - 5000 * bulge, "Shallow exact midpoint N", 1e-6);
            var samples = DrawingCurveSampler.Sample(shallow, 452 / 1.1);
            Near(samples.Points[0].E, 450000, "Shallow sampled start E", 1e-6);
            Near(samples.Points[^1].N, 7010000, "Shallow sampled end N", 1e-6);
            var emitted = 0;
            var shallowDefinition = TraversalTests.Definition(0, new DrawingGeometryNode(shallow));
            DrawingTraversal.Visit(TraversalTests.Scene(shallowDefinition), new HashSet<string> { "0" }, new DrawingBounds(449999.9, 6999999.9, 450000.1, 7000000.1), _ => emitted++);
            Check(emitted == 1, "Shallow vertex region not culled");
            var transformedShallow = shallow.Transform(new Affine2D(0, -3, 2, 0, 100, 200));
            var expectedStart = new Affine2D(0, -3, 2, 0, 100, 200).Apply(450000, 7000000);
            var transformedStart = transformedShallow.At(transformedShallow.StartAngle);
            Near(transformedStart.E, expectedStart.E, "Shallow transformed start E", 1e-6);
            Near(transformedStart.N, expectedStart.N, "Shallow transformed start N", 1e-6);
        }
        foreach (var magnitude in new[] { 1e20, 1e308 }) foreach (var reversed in new[] { false, true })
        {
            clipped = DrawingGeometry.ClipLine(reversed ? -magnitude : magnitude, reversed ? -magnitude : magnitude,
                reversed ? magnitude : -magnitude, reversed ? magnitude : -magnitude, new DrawingBounds(0, 0, 100, 100))!.Value;
            Near(clipped.E1, reversed ? 0 : 100, "Diagonal clip E1"); Near(clipped.N1, reversed ? 0 : 100, "Diagonal clip N1");
            Near(clipped.E2, reversed ? 100 : 0, "Diagonal clip E2"); Near(clipped.N2, reversed ? 100 : 0, "Diagonal clip N2");
            clipped = DrawingGeometry.ClipLine(reversed ? -magnitude : magnitude, reversed ? -magnitude : magnitude,
                reversed ? magnitude : -magnitude, reversed ? magnitude : -magnitude, new DrawingBounds(10, 10, 110, 110))!.Value;
            Near(clipped.E1, reversed ? 10 : 110, "Offset diagonal clip E1"); Near(clipped.N2, reversed ? 110 : 10, "Offset diagonal clip N2");
        }
        try { DrawingGeometry.FromBulge("0", 0, 0, 1, 0, double.NaN); throw new Exception("Accepted NaN bulge"); }
        catch (System.IO.InvalidDataException) { }
        Console.WriteLine("PASS analytic geometry, bulges, affine curves and owned graph");
    }
}
