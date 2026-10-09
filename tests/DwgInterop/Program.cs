using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.IO;
using ACadSharp.Tables;
using CSMath;
using KhitunGeo;
using System.Text;

if (args.Length == 2)
{
    // Local-only fixture check. Never publish the supplied survey or converted file.
    var result = DwgImportService.ConvertToDxf(File.ReadAllBytes(args[0]));
    File.WriteAllText(args[1], result.Dxf, new UTF8Encoding(false));
    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { notices = result.Notices, characters = result.Dxf.Length }));
    return;
}

void Check(bool condition, string label) { if (!condition) throw new Exception(label); Console.WriteLine("PASS " + label); }
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
foreach (var version in new[] { ACadVersion.AC1015, ACadVersion.AC1024, ACadVersion.AC1032 })
{
    var document = new CadDocument();
    document.Header.Version = version;
    document.Header.CodePage = "ANSI_1251";
    var layer = new Layer("Контур");
    document.Layers.Add(layer);
    document.Entities.Add(new Line { StartPoint = new XYZ(450200, 7000100, 0), EndPoint = new XYZ(450300, 7000200, 0), Layer = layer });
    using var file = new MemoryStream();
    DwgWriter.Write(file, document);
    var original = file.ToArray();
    var copy = original.ToArray();
    var converted = DwgImportService.ConvertToDxf(original);
    Check(original.SequenceEqual(copy), "DWG source unchanged " + version);
    using var input = new MemoryStream(Encoding.UTF8.GetBytes(converted.Dxf));
    var restored = DxfReader.Read(input);
    var line = restored.Entities.OfType<Line>().Single();
    Check(line.StartPoint.X == 450200 && line.StartPoint.Y == 7000100 && line.EndPoint.X == 450300 && line.EndPoint.Y == 7000200, "DWG coordinates retained " + version);
    Check(line.Layer.Name == "Контур", "Cyrillic layer retained " + version);
    Directory.CreateDirectory("test-artifacts");
    File.WriteAllText($"test-artifacts/dwg-{version}.dxf", converted.Dxf, new UTF8Encoding(false));
}
foreach (var bytes in new[] { Array.Empty<byte>(), Encoding.ASCII.GetBytes("notDWG"), Encoding.ASCII.GetBytes("AC1009"), new byte[DwgImportService.MaxInputBytes + 1] })
{
    try { DwgImportService.ConvertToDxf(bytes); throw new Exception("invalid DWG accepted"); }
    catch (InvalidDataException) { Console.WriteLine("PASS invalid or oversized DWG rejected"); }
}
try { DwgImportService.ConvertToDxf(Encoding.ASCII.GetBytes("AC1032broken")); throw new Exception("truncated DWG accepted"); }
catch (Exception ex) when (ex.Message != "truncated DWG accepted") { Console.WriteLine("PASS truncated DWG fails without a result"); }
