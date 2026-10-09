using System.IO;
using System.Diagnostics;
using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.Tables;
using ACadSharp.IO;
using CSMath;
using KhitunGeo.Native;

static class DrawingBenchmark
{
    public static void Run()
    {
        var folder = Path.Combine(Path.GetTempPath(), "khitun-benchmark-" + Guid.NewGuid()); Directory.CreateDirectory(folder);
        try
        {
            var doc = new CadDocument(ACadVersion.AC1032);
            var common = new BlockRecord("SharedGeometry");
            for (var i = 0; i < 100; i++) common.Entities.Add(new Line { StartPoint = new XYZ(i % 10, i / 10, 0), EndPoint = new XYZ(i % 10 + 1, i / 10 + 1, 0) });
            common.Entities.Add(new Circle { Center = new XYZ(5, 5, 0), Radius = 2 }); doc.BlockRecords.Add(common);
            var wrapper = new BlockRecord("Nested"); wrapper.Entities.Add(new Insert(common)); doc.BlockRecords.Add(wrapper);
            for (var i = 0; i < 2020; i++)
                doc.Entities.Add(new Insert(wrapper) { InsertPoint = i < 20 ? new XYZ((i % 5) * 10, (i / 5) * 10, 0) : new XYZ(1e6 + i * 10, 1e6, 0) });
            foreach (var extension in new[] { ".dwg", ".dxf" })
            {
                var path = Path.Combine(folder, "repeat" + extension);
                using (var output = File.Create(path)) { if (extension == ".dwg") DwgWriter.Write(output, doc); else DxfWriter.Write(output, doc, false); }
                _ = NativeDrawingReader.Read(path); // warm dependency/reader paths
                for (var run = 1; run <= 3; run++)
                {
                    var before = GC.GetTotalAllocatedBytes(true); var watch = Stopwatch.StartNew();
                    var scene = NativeDrawingReader.Read(path); watch.Stop();
                    var allocated = GC.GetTotalAllocatedBytes(true) - before;
                    var visible = scene.Layers.Select(l => l.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
                    var loadMs = watch.Elapsed.TotalMilliseconds;
                    foreach (var limited in new[] { false, true })
                    {
                        var delivered = 0; watch.Restart();
                        var stats = DrawingTraversal.Visit(scene, visible, limited ? new DrawingBounds(-5, -5, 105, 105) : null, _ => delivered++);
                        watch.Stop();
                        Console.WriteLine($"BENCH {extension} run={run} bytes={new FileInfo(path).Length} loadMs={loadMs:F2} allocatedBytes={allocated} definitions={scene.Definitions.Count} stored={scene.StoredElementCount} area={(limited ? "points" : "all")} visitMs={watch.Elapsed.TotalMilliseconds:F2} visited={stats.Visited} emitted={delivered} truncated={stats.Truncated}");
                        GeometryTests.Check(scene.Definitions.Count == 3 && scene.StoredElementCount == 2122, "Benchmark common geometry retained once");
                        if (limited) GeometryTests.Check(stats.Visited == 4060 && stats.Emitted == 2020 && !stats.Truncated, "Benchmark point-region culling");
                        else GeometryTests.Check(stats.Visited == 200000 && stats.Truncated, "Benchmark full-region bounded");
                    }
                }
            }
        }
        finally { Directory.Delete(folder, true); }
    }
}
