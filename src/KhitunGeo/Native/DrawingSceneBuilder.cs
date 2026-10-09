using System.IO;
using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.Tables;
using ACadSharp.Blocks;
using CSMath;

namespace KhitunGeo.Native;

internal sealed class DrawingSceneBuilder
{
    private readonly Dictionary<BlockRecord, DrawingDefinition> built = new();
    private readonly Dictionary<BlockRecord, int> heights = new();
    private readonly HashSet<BlockRecord> visiting = new();
    private int nextId, elements, skipped;
    public static DrawingScene Build(CadDocument document, int readerNotices)
    {
        var builder = new DrawingSceneBuilder();
        var root = builder.Define(document.ModelSpace, 0);
        var layers = document.Layers.Select(l => new DrawingLayer(l.Name, l.IsOn && !l.Flags.HasFlag(LayerFlags.Frozen)));
        return new(root, builder.built.Values, layers, builder.skipped, readerNotices);
    }
    private DrawingDefinition Define(BlockRecord block, int depth)
    {
        if (depth > 32) throw new InvalidDataException("Вложенность блоков больше 32.");
        if (visiting.Contains(block)) throw new InvalidDataException("Циклические ссылки блоков.");
        if (built.TryGetValue(block, out var cached))
        {
            if (depth + heights[block] > 32) throw new InvalidDataException("Вложенность блоков больше 32.");
            return cached;
        }
        visiting.Add(block);
        var id = nextId++; var nodes = new List<DrawingNode>(); var bounds = DrawingBounds.Empty; var height = 0;
        void Add(DrawingNode node, DrawingBounds b)
        {
            if (!b.IsEmpty && !b.IsFinite) throw new InvalidDataException("Некорректные границы геометрии.");
            if (++elements > 200000) throw new InvalidDataException("Больше 200 000 элементов геометрии и вставок. Разделите чертёж.");
            nodes.Add(node); bounds = bounds.Union(b);
        }
        void Primitive(DrawingPrimitive p)
        {
            if (!p.IsValid) throw new InvalidDataException("В чертеже обнаружена некорректная геометрия.");
            Add(new DrawingGeometryNode(p), p.Bounds);
        }
        void Polyline(string layer, IReadOnlyList<(double E, double N, double Bulge)> vertices, bool closed)
        {
            foreach (var v in vertices)
                if (!double.IsFinite(v.E) || !double.IsFinite(v.N) || !double.IsFinite(v.Bulge)) throw new InvalidDataException("Некорректная полилиния.");
            if (vertices.Count == 1) { Primitive(new(DrawingKind.Point, layer, vertices[0].E, vertices[0].N)); return; }
            var count = closed ? vertices.Count : vertices.Count - 1;
            for (var i = 0; i < count; i++)
            {
                var a = vertices[i]; var z = vertices[(i + 1) % vertices.Count];
                Primitive(DrawingGeometry.FromBulge(layer, a.E, a.N, z.E, z.N, a.Bulge));
            }
        }
        foreach (var entity in block.Entities)
        {
            if (entity.IsInvisible) { skipped++; continue; }
            var layer = entity.Layer?.Name ?? "0";
            switch (entity)
            {
                case Line line:
                    Primitive(new(DrawingKind.Line, layer, line.StartPoint.X, line.StartPoint.Y, line.EndPoint.X, line.EndPoint.Y)); break;
                case Arc arc when IsPlanar(arc.Normal):
                    Primitive(new(DrawingKind.Arc, layer, arc.Center.X, arc.Center.Y, Radius: arc.Radius, StartAngle: arc.StartAngle, EndAngle: arc.EndAngle)); break;
                case Circle circle when IsPlanar(circle.Normal):
                    Primitive(new(DrawingKind.Circle, layer, circle.Center.X, circle.Center.Y, Radius: circle.Radius)); break;
                case ACadSharp.Entities.Point point:
                    Primitive(new(DrawingKind.Point, layer, point.Location.X, point.Location.Y)); break;
                case LwPolyline lw when IsPlanar(lw.Normal):
                    Polyline(layer, lw.Vertices.Select(v => (v.Location.X, v.Location.Y, v.Bulge)).ToArray(), lw.IsClosed); break;
                case Polyline2D poly when IsPlanar(poly.Normal) && (poly.Flags & (PolylineFlags.CurveFit | PolylineFlags.SplineFit | PolylineFlags.Polyline3D | PolylineFlags.PolygonMesh | PolylineFlags.PolyfaceMesh)) == 0:
                    Polyline(layer, poly.Vertices.Select(v => (v.Location.X, v.Location.Y, v.Bulge)).ToArray(), poly.IsClosed); break;
                case Insert insert when IsPlanar(insert.Normal) && !insert.IsMultiple && insert.SpatialFilter is null:
                    var target = insert.Block;
                    if (target is null || target.IsUnloaded || (target.BlockEntity.Flags & (BlockTypeFlags.XRef | BlockTypeFlags.XRefOverlay)) != 0)
                    { skipped++; break; }
                    var transform = InsertTransform(insert);
                    var definition = Define(target, depth + 1);
                    height = Math.Max(height, heights[target] + 1);
                    Add(new DrawingInsertNode(definition.Id, transform, layer), definition.Bounds.Transform(transform));
                    break;
                default: skipped++; break;
            }
        }
        visiting.Remove(block);
        var result = new DrawingDefinition(id, block.Name, nodes, bounds);
        built.Add(block, result); heights.Add(block, height); return result;
    }
    private static bool IsPlanar(XYZ n) => Math.Abs(n.X) < 1e-12 && Math.Abs(n.Y) < 1e-12 && Math.Abs(n.Z - 1) < 1e-12;
    private static Affine2D InsertTransform(Insert insert)
    {
        var c = Math.Cos(insert.Rotation); var s = Math.Sin(insert.Rotation); var b = insert.Block.BlockEntity.BasePoint;
        var a = c * insert.XScale; var m = -s * insert.YScale;
        var d = s * insert.XScale; var n = c * insert.YScale;
        var t = new Affine2D(a, m, d, n, insert.InsertPoint.X - a * b.X - m * b.Y, insert.InsertPoint.Y - d * b.X - n * b.Y);
        if (!t.IsFinite) throw new InvalidDataException("Некорректное преобразование блока.");
        return t;
    }
}
