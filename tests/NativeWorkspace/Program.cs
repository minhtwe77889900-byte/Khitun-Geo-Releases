using KhitunGeo.Native;

static void Check(bool condition, string name)
{
    if (!condition) throw new Exception(name);
}

var workspace = new PointWorkspace();
Check(!workspace.Undo(), "Empty undo");
var original = new SurveyPoint("7", 7000100, 450200, -12.4, "Bottom");
workspace.ReplacePoints(new[] { original, original with { Name = "8", Height = null } });
workspace.SetPoint(0, original with { X = 1, Height = -8.2 });
Check(workspace.Undo() && workspace.Points[0] == original, "Edit undo restores every field");
workspace.OffsetHeight(-12);
Check(workspace.Points[0].Height == -24.4 && workspace.Points[1].Height is null, "Offset all heights");
Check(workspace.Undo() && workspace.Points[0] == original, "Height undo");
var heightPoints = new[] { original, original with { Name = "8", Height = 5.25 }, original with { Name = "9", Height = null } };
Check(HeightCalculator.Apply(heightPoints, HeightOperation.SetAll, 74.65)!.All(p => p.Height == 74.65), "Assign calculated absolute height to all points");
Check(HeightCalculator.Apply(heightPoints, HeightOperation.SubtractFromAll, 12)![0].Height == -24.4, "Subtract depth from every existing height");
Check(HeightCalculator.Apply(heightPoints, HeightOperation.SubtractFromAll, 12)![2].Height is null, "Subtraction preserves missing height");
Check(HeightCalculator.AbsoluteMinusDepth(86.65, 12) == 74.65, "Absolute elevation minus lake depth");
try { HeightCalculator.Apply(heightPoints, HeightOperation.AddToAll, double.NaN); throw new Exception("Accepted nonfinite height input"); }
catch (ArgumentOutOfRangeException) { }
workspace.ReplacePoints(heightPoints);
workspace.ReplacePoints(HeightCalculator.Apply(workspace.Points, HeightOperation.SetAll, HeightCalculator.AbsoluteMinusDepth(86.65, 12))!);
Check(workspace.Points.All(p => p.Height == 74.65), "Bulk height calculation is one workspace action");
Check(workspace.Undo() && workspace.Points.SequenceEqual(heightPoints), "Bulk height calculation undo restores all points");
try { workspace.SetPoint(0, original with { X = double.NaN }); throw new Exception("Accepted NaN"); }
catch (ArgumentException) { Check(workspace.Points[0] == original, "Invalid edit preserves values"); }
workspace.ReplacePoints(Array.Empty<SurveyPoint>());
workspace.OffsetHeight(1);
Check(workspace.Points.Count == 0, "Empty offset");
var tab = TabularPaste.Parse("7\t7000100\t450200\t-12,4\tBottom");
Check(tab[0] == original, "Spreadsheet decimal comma and negative height");
var csv = TabularPaste.Parse("Name;X;Y;Z;Description\n8;1;2;;\"A; B\"");
Check(csv[0].Height is null && csv[0].Description == "A; B", "CSV quoting, header and missing height");
Check(TabularPaste.Parse("9,1.5,2.5,-3.5,Edge")[0].X == 1.5, "Comma CSV");
Check(TabularPaste.Parse("7,1,2,3,\"A; B\"")[0].Description == "A; B", "Delimiter inside quoted CSV field");
Check(TabularPaste.Parse("").Count == 0, "Empty paste");
try { TabularPaste.Parse("7\tbad\t2\t3"); throw new Exception("Accepted invalid paste"); }
catch (FormatException) { }
try { TabularPaste.Parse("7\tNaN\t2\t3"); throw new Exception("Accepted nonfinite paste"); }
catch (FormatException) { }
Check(TabularPaste.Parse("10\t\t2\t3")[0].X is null, "Cleared coordinate stays missing");
workspace.ReplacePoints(new[] { original });
workspace.SwapXY();
Check(workspace.Points[0] == original with { X = original.Y, Y = original.X }, "Swap preserves height and description");
Check(workspace.Undo() && workspace.Points[0] == original, "Swap undo");
var described = original with { Description = "Дно; \"озеро\"\r\nвторая строка", Height = null };
Check(TabularPaste.Parse(PointFileService.ToCsv(new[] { described }))[0] == described, "CSV roundtrip preserves multiline Unicode and missing height");
var spaced = original with { Name = " 7 ", Description = "  Дно  " };
Check(TabularPaste.Parse(PointFileService.ToCsv(new[] { spaced }))[0] == spaced, "CSV preserves intentional whitespace");
workspace.OffsetHeight(86.65);
var exported = TabularPaste.Parse(PointFileService.ToCsv(workspace.Points));
Check(exported[0] == workspace.Points[0], "Export uses edited current values");
var temp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "khitun-native-" + Guid.NewGuid());
System.IO.Directory.CreateDirectory(temp);
try
{
    var file = System.IO.Path.Combine(temp, "points.csv");
    PointFileService.SaveCsv(file, new[] { described });
    Check(PointFileService.Read(file)[0] == described, "File UTF8 roundtrip");
    try { PointFileService.SaveCsv(file, new[] { described with { Y = double.PositiveInfinity } }); throw new Exception("Accepted infinity export"); }
    catch (ArgumentException) { Check(PointFileService.Read(file)[0] == described, "Invalid export preserves existing file"); }
    Check(System.IO.Directory.GetFiles(temp).Length == 1, "No leftover temporary files");
}
finally { System.IO.Directory.Delete(temp, true); }
Console.WriteLine("Native workspace, paste, swap and file checks passed.");
