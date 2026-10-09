using System.Collections.ObjectModel;
namespace KhitunGeo.Native;

internal enum DrawingKind { Line, Circle, Arc, Point, Curve }
internal sealed record DrawingPrimitive(DrawingKind Kind, string Layer, double E1, double N1,
    double E2 = 0, double N2 = 0, double Radius = 0, double StartAngle = 0, double EndAngle = 0,
    double? Sweep = null, double UE = 0, double UN = 0, double VE = 0, double VN = 0, double? AnchorE = null, double? AnchorN = null)
{
    public bool IsCurve => Kind is DrawingKind.Circle or DrawingKind.Arc or DrawingKind.Curve;
    public double SweepRadians => Sweep ?? (Kind == DrawingKind.Circle ? DrawingGeometry.Tau
        : DrawingGeometry.Normalize(DrawingGeometry.Normalize(EndAngle) - DrawingGeometry.Normalize(StartAngle)));
    public (double E, double N) U => Kind == DrawingKind.Curve ? (UE, UN) : (Radius, 0);
    public (double E, double N) V => Kind == DrawingKind.Curve ? (VE, VN) : (0, Radius);
    public float ScreenStartDegrees => (float)(-DrawingGeometry.Normalize(StartAngle) * 180 / Math.PI);
    public float ScreenSweepDegrees => (float)(-SweepRadians * 180 / Math.PI);
    public (double E, double N) At(double angle)
    {
        var c = Math.Cos(angle); var s = Math.Sin(angle);
        if (AnchorE is double ae && AnchorN is double an)
        {
            var halfSin = Math.Sin(angle / 2); var cosineDelta = -2 * halfSin * halfSin;
            return (ae + U.E * cosineDelta + V.E * s, an + U.N * cosineDelta + V.N * s);
        }
        return (E1 + U.E * c + V.E * s, N1 + U.N * c + V.N * s);
    }
    public bool IsValid => double.IsFinite(E1) && double.IsFinite(N1) && double.IsFinite(E2) && double.IsFinite(N2)
        && double.IsFinite(Radius) && Radius >= 0 && double.IsFinite(StartAngle) && double.IsFinite(EndAngle)
        && double.IsFinite(SweepRadians) && Math.Abs(SweepRadians) <= DrawingGeometry.Tau + 1e-12
        && double.IsFinite(UE) && double.IsFinite(UN) && double.IsFinite(VE) && double.IsFinite(VN)
        && ((!AnchorE.HasValue && !AnchorN.HasValue) || (AnchorE is double ae && AnchorN is double an && double.IsFinite(ae) && double.IsFinite(an) && StartAngle == 0))
        && Bounds.IsFinite;
    public DrawingBounds Bounds
    {
        get
        {
            var b = DrawingBounds.Empty.Include(E1, N1);
            if (Kind == DrawingKind.Line) return b.Include(E2, N2);
            if (!IsCurve) return b;
            var start = DrawingGeometry.Normalize(StartAngle);
            var a = At(start); var z = At(start + SweepRadians);
            b = DrawingBounds.Empty.Include(a.E, a.N).Include(z.E, z.N);
            foreach (var angle in new[] { Math.Atan2(V.E, U.E), Math.Atan2(-V.E, -U.E),
                Math.Atan2(V.N, U.N), Math.Atan2(-V.N, -U.N) })
                if (DrawingGeometry.OnSweep(angle, start, SweepRadians)) { var p = At(angle); b = b.Include(p.E, p.N); }
            return b;
        }
    }
    public DrawingPrimitive Transform(Affine2D t)
    {
        var p = t.Apply(E1, N1);
        if (!IsCurve)
        {
            var q = t.Apply(E2, N2);
            return this with { E1 = p.E, N1 = p.N, E2 = q.E, N2 = q.N };
        }
        var u = t.Vector(U.E, U.N); var v = t.Vector(V.E, V.N);
        var anchor = AnchorE is double ae && AnchorN is double an ? t.Apply(ae, an) : ((double E, double N)?)null;
        return this with { Kind = DrawingKind.Curve, E1 = p.E, N1 = p.N, UE = u.E, UN = u.N, VE = v.E, VN = v.N,
            StartAngle = DrawingGeometry.Normalize(StartAngle), Sweep = SweepRadians, AnchorE = anchor?.E, AnchorN = anchor?.N };
    }
}
internal abstract record DrawingNode;
internal sealed record DrawingGeometryNode(DrawingPrimitive Primitive) : DrawingNode;
internal sealed record DrawingInsertNode(int DefinitionId, Affine2D Transform, string Layer) : DrawingNode;
internal sealed record DrawingLayer(string Name, bool InitiallyVisible);
internal sealed class DrawingDefinition
{
    public int Id { get; }
    public string Name { get; }
    public IReadOnlyList<DrawingNode> Nodes { get; }
    public DrawingBounds Bounds { get; }
    public DrawingDefinition(int id, string name, IEnumerable<DrawingNode> nodes, DrawingBounds bounds)
    { Id = id; Name = name; Nodes = Array.AsReadOnly(nodes.ToArray()); Bounds = bounds; }
}
internal sealed class DrawingScene
{
    public DrawingDefinition Root { get; }
    public IReadOnlyDictionary<int, DrawingDefinition> Definitions { get; }
    public IReadOnlyList<DrawingLayer> Layers { get; }
    public int StoredElementCount => Definitions.Values.Sum(d => d.Nodes.Count);
    public int SkippedEntities { get; }
    public int ReaderNotices { get; }
    public DrawingScene(DrawingDefinition root, IEnumerable<DrawingDefinition> definitions, IEnumerable<DrawingLayer> layers, int skipped, int notices)
    {
        Root = root; Definitions = new ReadOnlyDictionary<int, DrawingDefinition>(definitions.ToDictionary(d => d.Id));
        Layers = Array.AsReadOnly(layers.ToArray()); SkippedEntities = skipped; ReaderNotices = notices;
    }
}
