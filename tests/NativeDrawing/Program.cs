using System.IO;
using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.IO;
using ACadSharp.Tables;
using CSMath;
using KhitunGeo.Native;
using static GeometryTests;

GeometryTests.Run();
ReaderTests.Run();
TraversalTests.Run();
var extreme = new DrawingPrimitive(DrawingKind.Arc, "0", 0, 0, Radius: 1, StartAngle: double.MaxValue, EndAngle: -double.MaxValue);
Check(float.IsFinite(extreme.ScreenStartDegrees) && float.IsFinite(extreme.ScreenSweepDegrees), "Finite GDI angles for finite extreme radians");
System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
var folder = Path.Combine(Path.GetTempPath(), "khitun-drawing-" + Guid.NewGuid());
Directory.CreateDirectory(folder);
try
{
    foreach (var version in new[] { ACadVersion.AC1015, ACadVersion.AC1024, ACadVersion.AC1032 })
    {
        var doc = new CadDocument();
        doc.Header.Version = version;
        doc.Header.CodePage = "ANSI_1251";
        var layer = new Layer("Контур");
        doc.Layers.Add(layer);
        doc.Entities.Add(new Line { StartPoint = new XYZ(450200, 7000100, -12), EndPoint = new XYZ(450300, 7000200, -8), Layer = layer });
        doc.Entities.Add(new Circle { Center = new XYZ(450250, 7000150, 0), Radius = 10, Layer = layer });
        doc.Entities.Add(new Arc { Center = new XYZ(450250, 7000150, 0), Radius = 5, StartAngle = 0, EndAngle = Math.PI / 2, Layer = layer });
        doc.Entities.Add(new ACadSharp.Entities.Point { Location = new XYZ(450210, 7000120, -10), Layer = layer });
        doc.Entities.Add(new Circle { Center = new XYZ(450250, 7000150, 0), Radius = 2, Normal = new XYZ(0, 1, 0), Layer = layer });
        var file = Path.Combine(folder, version + ".dwg");
        using (var output = File.Create(file)) DwgWriter.Write(output, doc);
        var before = File.ReadAllBytes(file);
        var drawing = NativeDrawingReader.Read(file);
        var primitives = drawing.Root.Nodes.OfType<DrawingGeometryNode>().Select(n => n.Primitive).ToArray();
        Check(primitives.Length == 4, "Primitive count " + version);
        var line = primitives.Single(p => p.Kind == DrawingKind.Line);
        Check(line.E1 == 450200 && line.N1 == 7000100 && line.E2 == 450300 && line.N2 == 7000200, "Absolute axes " + version);
        Check(line.Layer == "Контур", "Cyrillic layer " + version);
        Check(drawing.SkippedEntities == 1, "Tilted OCS reported, never drawn as WCS");
        Check(primitives.Single(p => p.Kind == DrawingKind.Point).N1 == 7000120, "CAD point northing");
        Check(line.Bounds == new DrawingBounds(450200d, 7000100d, 450300d, 7000200d), "Line bounds");
        Check(primitives.Single(p => p.Kind == DrawingKind.Circle).Radius == 10, "Analytic circle");
        Check(primitives.Single(p => p.Kind == DrawingKind.Arc).EndAngle == Math.PI / 2, "Analytic arc");
        Check(File.ReadAllBytes(file).SequenceEqual(before), "Source unchanged");
        var dxf = Path.Combine(folder, version + ".dxf");
        using (var output = File.Create(dxf)) DxfWriter.Write(output, doc, false);
        Check(NativeDrawingReader.Read(dxf).StoredElementCount == 4, "DXF geometry");
    }
    var invalid = Path.Combine(folder, "invalid.dwg");
    File.WriteAllText(invalid, "notDWG");
    try { NativeDrawingReader.Read(invalid); throw new Exception("Accepted invalid DWG"); }
    catch (Exception ex) when (ex.Message != "Accepted invalid DWG") { }
    Console.WriteLine("Native DWG/DXF geometry checks passed for 2000, 2010 and 2018.");
}
finally { Directory.Delete(folder, true); }
if (args.Contains("--benchmark")) DrawingBenchmark.Run();
