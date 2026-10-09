using System.IO;
using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.IO;
using ACadSharp.Tables;
using CSMath;
using KhitunGeo.Native;
using static GeometryTests;

static class ReaderTests
{
    internal static List<DrawingPrimitive> Expand(DrawingScene scene)
    {
        var result = new List<DrawingPrimitive>();
        void Walk(DrawingDefinition d, Affine2D t)
        {
            foreach (var n in d.Nodes)
                if (n is DrawingGeometryNode g) result.Add(g.Primitive.Transform(t));
                else if (n is DrawingInsertNode i) Walk(scene.Definitions[i.DefinitionId], t.Compose(i.Transform));
        }
        Walk(scene.Root, Affine2D.Identity); return result;
    }
    public static void Run()
    {
        var folder = Path.Combine(Path.GetTempPath(), "khitun-blocks-" + Guid.NewGuid()); Directory.CreateDirectory(folder);
        try
        {
            foreach (var version in new[] { ACadVersion.AC1015, ACadVersion.AC1024, ACadVersion.AC1032 })
            {
                var doc = new CadDocument(version);
                doc.Header.CodePage = "ANSI_1251";
                var layer = new Layer("Контур"); var off = new Layer("Выключен") { IsOn = false };
                var frozen = new Layer("Заморожен") { Flags = LayerFlags.Frozen };
                doc.Layers.Add(layer); doc.Layers.Add(off); doc.Layers.Add(frozen);
                var lw = new LwPolyline();
                lw.Vertices.Add(new(0, 0) { Bulge = 1 }); lw.Vertices.Add(new(10, 0) { Bulge = -1 }); lw.Vertices.Add(new(20, 0));
                doc.Entities.Add(lw);
                var closed = new LwPolyline { IsClosed = true };
                closed.Vertices.Add(new(30, 0)); closed.Vertices.Add(new(40, 0) { Bulge = 1 }); doc.Entities.Add(closed);
                var poly = new Polyline2D();
                poly.Vertices.Add(new Vertex2D(new XYZ(50, 0, 0)) { Bulge = -1 });
                poly.Vertices.Add(new Vertex2D(new XYZ(60, 0, 0))); poly.Vertices.Add(new Vertex2D(new XYZ(60, 0, 0))); doc.Entities.Add(poly);
                var b = new BlockRecord("Геометрия"); b.BlockEntity.BasePoint = new XYZ(10, 20, 0);
                b.Entities.Add(new ACadSharp.Entities.Point { Location = new XYZ(11, 22, 0) });
                b.Entities.Add(new Circle { Center = new XYZ(10, 20, 0), Radius = 1, Layer = layer }); doc.BlockRecords.Add(b);
                b.Entities.Add(new Arc { Center = new XYZ(10, 20, 0), Radius = 2, StartAngle = 0, EndAngle = Math.PI / 2, Layer = layer });
                var outer = new BlockRecord("Вложенный");
                outer.Entities.Add(new Insert(b) { InsertPoint = new XYZ(100, 200, 0), XScale = 2, YScale = 3, Rotation = Math.PI / 2 }); doc.BlockRecords.Add(outer);
                doc.Entities.Add(new Insert(b) { InsertPoint = new XYZ(100, 200, 0), XScale = 2, YScale = 3, Rotation = Math.PI / 2, Layer = layer });
                doc.Entities.Add(new Insert(b) { InsertPoint = new XYZ(200, 300, 0), Layer = off });
                doc.Entities.Add(new Insert(outer) { InsertPoint = new XYZ(1000, 1000, 0), XScale = -1, YScale = 2 });
                foreach (var extension in new[] { ".dwg", ".dxf" })
                {
                    var path = Path.Combine(folder, version + extension);
                    using (var stream = File.Create(path)) { if (extension == ".dwg") DwgWriter.Write(stream, doc); else DxfWriter.Write(stream, doc, false); }
                    var before = File.ReadAllBytes(path); var scene = NativeDrawingReader.Read(path);
                    Check(scene.Definitions.Count == 3, "Shared definitions " + version + extension);
                    Check(scene.StoredElementCount == 13, "Shared geometry count " + scene.StoredElementCount);
                    var expanded = Expand(scene);
                    Check(expanded.Count == 15, "Expanded primitives");
                    var direct = scene.Root.Nodes.OfType<DrawingGeometryNode>().Select(n => n.Primitive).ToArray();
                    var fixture = version + extension;
                    VerifyArc(direct[0], (0, 0), (5, -5), (10, 0), new(0, -5, 10, 0), 1, "Imported positive bulge " + fixture);
                    VerifyArc(direct[1], (10, 0), (15, 5), (20, 0), new(10, 0, 20, 5), -1, "Imported negative bulge " + fixture);
                    Check(direct[2].Kind == DrawingKind.Line, "Imported straight closing-outline segment " + fixture);
                    Near(direct[2].E1, 30, "Outline starts at first vertex"); Near(direct[2].E2, 40, "Outline ends at last vertex");
                    VerifyArc(direct[3], (40, 0), (35, 5), (30, 0), new(30, 0, 40, 5), 1, "Imported last-to-first bulge " + fixture);
                    VerifyArc(direct[4], (50, 0), (55, 5), (60, 0), new(50, 0, 60, 5), -1, "Imported legacy POLYLINE bulge " + fixture);
                    Check(direct[5].Kind == DrawingKind.Point, "Imported coincident legacy vertices " + fixture);
                    Near(direct[5].E1, 60, "Coincident point E"); Near(direct[5].N1, 0, "Coincident point N");
                    Check(expanded.Any(p => p.Kind == DrawingKind.Point && Math.Abs(p.E1 - 94) < 1e-7 && Math.Abs(p.N1 - 202) < 1e-7), "Insert base/rotation");
                    Check(expanded.Any(p => p.Kind == DrawingKind.Point && Math.Abs(p.E1 - 906) < 1e-7 && Math.Abs(p.N1 - 1404) < 1e-7), "Nested mirror/scale");
                    var nestedCurve = expanded.Single(p => p.IsCurve && Math.Abs(p.E1 - 900) < 1e-7 && Math.Abs(p.SweepRadians - 2 * Math.PI) < 1e-8);
                    Near(nestedCurve.Bounds.MinE, 897, "Nested ellipse minE"); Near(nestedCurve.Bounds.MaxN, 1404, "Nested ellipse maxN");
                    // Local quarter-circle (12,20) -> (10,22), through two
                    // inserts with a base offset, rotation, mirror and scale.
                    var nestedArc = expanded.Single(p => p.IsCurve && Math.Abs(p.E1 - 900) < 1e-7 && Math.Abs(p.SweepRadians - 2 * Math.PI) > 1e-8);
                    VerifyArc(nestedArc, (900, 1408), (900 + 3 * Math.Sqrt(2), 1400 + 4 * Math.Sqrt(2)),
                        (906, 1400), new(900, 1400, 906, 1408), -1, "Imported reflected nested quarter-arc " + fixture);
                    var visible = scene.Layers.Select(l => l.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
                    var traversed = new List<DrawingPrimitive>();
                    DrawingTraversal.Visit(scene, visible, new DrawingBounds(899, 1399, 907, 1409), traversed.Add);
                    VerifyArc(traversed.Single(p => p.IsCurve && Math.Abs(p.SweepRadians - Math.PI / 2) < 1e-8),
                        (900, 1408), (900 + 3 * Math.Sqrt(2), 1400 + 4 * Math.Sqrt(2)), (906, 1400),
                        new(900, 1400, 906, 1408), -1, "Visible imported quarter-arc " + fixture);
                    Check(!scene.Layers.Single(l => l.Name == off.Name).InitiallyVisible && !scene.Layers.Single(l => l.Name == frozen.Name).InitiallyVisible, "Off/frozen layers");
                    Check(File.ReadAllBytes(path).SequenceEqual(before), "Read-only source");
                }
            }
            var cyclic = new CadDocument(); var cb = new BlockRecord("Cycle"); cyclic.BlockRecords.Add(cb);
            cyclic.Entities.Add(new Insert(cb)); cb.Entities.Add(new Insert(cb));
            Reject(cyclic, "Cycle");
            var deep = new CadDocument(); BlockRecord? previous = null;
            for (var i = 0; i < 33; i++) { var b = new BlockRecord("Depth" + i); deep.BlockRecords.Add(b); if (previous is not null) b.Entities.Add(new Insert(previous)); previous = b; }
            deep.Entities.Add(new Insert(previous!)); Reject(deep, "Depth 33");
            var bad = new CadDocument(); bad.Entities.Add(new Line { StartPoint = new XYZ(double.NaN, 0, 0) }); Reject(bad, "NaN geometry");
            var tooMany = new CadDocument(); var many = new LwPolyline();
            for (var i = 0; i < 200002; i++) many.Vertices.Add(new(i, 0)); tooMany.Entities.Add(many); Reject(tooMany, "Element limit");
            var unsupported = new CadDocument();
            unsupported.Entities.Add(new Polyline3D(new[] { new XYZ(0, 0, 0), new XYZ(1, 1, 1) }));
            unsupported.Entities.Add(new LwPolyline { Normal = new XYZ(0, 1, 0) });
            var xref = new BlockRecord("MissingXref"); xref.BlockEntity.Flags = ACadSharp.Blocks.BlockTypeFlags.XRef; xref.BlockEntity.XRefPath = "missing.dwg";
            unsupported.BlockRecords.Add(xref); unsupported.Entities.Add(new Insert(xref));
            Check(DrawingSceneBuilder.Build(unsupported, 0).SkippedEntities == 3, "Unsupported and missing xref reported");
            Console.WriteLine("PASS real DWG/DXF imported arc endpoints/midpoints/bounds/direction, closing bulges, reflected nested arcs, shared blocks, layers and reader limits");
        }
        finally { Directory.Delete(folder, true); }
    }
    private static void VerifyArc(DrawingPrimitive arc, (double E, double N) expectedStart,
        (double E, double N) expectedMid, (double E, double N) expectedEnd, DrawingBounds bounds, int direction, string name)
    {
        Check(arc.IsCurve, name + " is a curve");
        var start = arc.At(arc.StartAngle); var mid = arc.At(arc.StartAngle + arc.SweepRadians / 2);
        var end = arc.At(arc.StartAngle + arc.SweepRadians);
        Near(start.E, expectedStart.E, name + " start E"); Near(start.N, expectedStart.N, name + " start N");
        Near(mid.E, expectedMid.E, name + " midpoint E"); Near(mid.N, expectedMid.N, name + " midpoint N");
        Near(end.E, expectedEnd.E, name + " end E"); Near(end.N, expectedEnd.N, name + " end N");
        Near(arc.Bounds.MinE, bounds.MinE, name + " bound minE"); Near(arc.Bounds.MinN, bounds.MinN, name + " bound minN");
        Near(arc.Bounds.MaxE, bounds.MaxE, name + " bound maxE"); Near(arc.Bounds.MaxN, bounds.MaxN, name + " bound maxN");
        var cross = (mid.E - start.E) * (end.N - start.N) - (mid.N - start.N) * (end.E - start.E);
        Check(Math.Sign(cross) == direction, name + " world-space direction");
    }
    private static void Reject(CadDocument doc, string name)
    {
        try { DrawingSceneBuilder.Build(doc, 0); throw new Exception("Accepted " + name); }
        catch (InvalidDataException) { }
    }
}
