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
var absoluteMinus = HeightCalculator.Apply(heightPoints, HeightOperation.AbsoluteMinusPoint, 65.635);
Check(Math.Abs(absoluteMinus[0].Height!.Value - 78.035) < 1e-10 && Math.Abs(absoluteMinus[1].Height!.Value - 60.385) < 1e-10, "Absolute mark minus each point's Z (operand order)");
Check(absoluteMinus.Select(p => (p.Name, p.X, p.Y, p.Description)).SequenceEqual(heightPoints.Select(p => (p.Name, p.X, p.Y, p.Description))), "Height operation preserves identifiers, XY and descriptions");
Check(Math.Abs(HeightCalculator.Apply(new[] { original with { Height = 12 } }, HeightOperation.AbsoluteMinusPoint, 65.635)[0].Height!.Value - 53.635) < 1e-10, "Absolute 65.635 minus depth 12 equals 53.635");
Check(absoluteMinus[2].Height is null, "Absolute height operations preserve missing Z");
var absolutePlus = HeightCalculator.Apply(heightPoints, HeightOperation.AbsolutePlusPoint, 65.635);
Check(Math.Abs(absolutePlus[0].Height!.Value - 53.235) < 1e-10 && Math.Abs(absolutePlus[1].Height!.Value - 70.885) < 1e-10, "Absolute mark plus each point's Z");
workspace.ReplacePoints(heightPoints);
workspace.ReplacePoints(absoluteMinus);
Check(workspace.Undo() && workspace.Points.SequenceEqual(heightPoints), "Absolute height operation undoes the entire table in one action");
try { HeightCalculator.Apply(new[] { original with { Height = double.MaxValue } }, HeightOperation.AbsolutePlusPoint, double.MaxValue); throw new Exception("Accepted height overflow"); }
catch (ArgumentOutOfRangeException) { }
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
Check(TabularPaste.Parse("12\t65\t84")[0] == new SurveyPoint("12", 65, 84, null, ""), "Paste accepts omitted height");
Check(TabularPaste.Parse("Описание;Y;Имя;X\nДно;84;12;65")[0] == new SurveyPoint("12", 65, 84, null, "Дно"), "Header mapping supports reordered columns and omitted height");
Check(TabularPaste.ParseCells("46,5\r\n47,25").Select(r => r[0]).SequenceEqual(new[] { "46,5", "47,25" }), "Paste reads a single column of decimal-comma values");
var heightPaste = TabularPaste.ApplyCells(new[] { original }, TabularPaste.ParseCells("46,5\r\n47,25"), 0, 3);
Check(heightPaste[0] == original with { Height = 46.5 } && heightPaste[1] == new SurveyPoint("2", null, null, 47.25, ""), "Single-column paste fills the selected height cells and adds rows");
try { TabularPaste.ApplyCells(new[] { original }, new[] { new[] { "bad" } }, 0, 3); throw new Exception("Accepted an invalid pasted height"); }
catch (FormatException) { Check(original.Height == -12.4, "Invalid cell paste does not change source points"); }
Check(TabularPaste.Parse("9,1.5,2.5,-3.5,Edge")[0].X == 1.5, "Comma CSV");
Check(TabularPaste.Parse("7,1,2,3,\"A; B\"")[0].Description == "A; B", "Delimiter inside quoted CSV field");
Check(JsonPointFileReader.Parse("[{\"no\":\"A1\",\"x\":65.1,\"y\":84.2,\"z\":-3.5,\"description\":\"Дно\"}]")[0] == new SurveyPoint("A1", 65.1, 84.2, -3.5, "Дно"), "JSON point array import");
Check(JsonPointFileReader.Parse("{\"data\":[[\"B2\",65,84]]}")[0] == new SurveyPoint("B2", 65, 84, null, ""), "JSON data array accepts omitted height");
Check(JsonPointFileReader.Parse("{\"type\":\"FeatureCollection\",\"features\":[{\"type\":\"Feature\",\"properties\":{\"name\":\"G1\",\"description\":\"Контроль\"},\"geometry\":{\"type\":\"Point\",\"coordinates\":[84,65,-2]}}]}")[0] == new SurveyPoint("G1", 65, 84, -2, "Контроль"), "GeoJSON coordinates map longitude, latitude, height correctly");
var pointInfo = "{\"pointInfoArray\":[[\"Точка #: 101\"],[\"Широта: 65,25 Долгота: 84,5\"],[\"не задано\"],[\"-12,4\"],[\"Карьер\"],[\"Участок 2\"],[\"\"],[\"\"],[\"Пикет\"]]}";
Check(JsonPointFileReader.Parse(pointInfo)[0] == new SurveyPoint("101", 65.25, 84.5, -12.4, "Карьер · Участок 2 · Пикет"), "Legacy pointInfoArray JSON import with Russian coordinate labels");
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
    var xlsx = System.IO.Path.Combine(temp, "points.xlsx");
    using (var archive = System.IO.Compression.ZipFile.Open(xlsx, System.IO.Compression.ZipArchiveMode.Create))
    {
        static void Entry(System.IO.Compression.ZipArchive a, string name, string value)
        {
            using var writer = new System.IO.StreamWriter(a.CreateEntry(name).Open(), new System.Text.UTF8Encoding(false));
            writer.Write(value);
        }
        Entry(archive, "xl/workbook.xml", "<workbook xmlns='http://schemas.openxmlformats.org/spreadsheetml/2006/main' xmlns:r='http://schemas.openxmlformats.org/officeDocument/2006/relationships'><sheets><sheet name='Points' sheetId='1' r:id='rId1'/><sheet name='Other' sheetId='2' r:id='rId2'/></sheets></workbook>");
        Entry(archive, "xl/_rels/workbook.xml.rels", "<Relationships xmlns='http://schemas.openxmlformats.org/package/2006/relationships'><Relationship Id='rId1' Target='worksheets/sheet1.xml' Type='http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet'/><Relationship Id='rId2' Target='worksheets/sheet2.xml' Type='http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet'/></Relationships>");
        Entry(archive, "xl/sharedStrings.xml", "<sst xmlns='http://schemas.openxmlformats.org/spreadsheetml/2006/main'><si><t>Name</t></si><si><t>X</t></si><si><t>Y</t></si><si><t>Height</t></si><si><t>Description</t></si><si><t>Дно</t></si></sst>");
        Entry(archive, "xl/worksheets/sheet1.xml", "<worksheet xmlns='http://schemas.openxmlformats.org/spreadsheetml/2006/main'><sheetData><row r='1'><c r='A1' t='s'><v>0</v></c><c r='B1' t='s'><v>1</v></c><c r='C1' t='s'><v>2</v></c><c r='D1' t='s'><v>3</v></c><c r='E1' t='s'><v>4</v></c></row><row r='2'><c r='A2' t='inlineStr'><is><t>7</t></is></c><c r='B2'><v>65.5</v></c><c r='C2'><v>84.25</v></c><c r='D2'><v>-12.4</v></c><c r='E2' t='s'><v>5</v></c></row><row r='3'><c r='A3' t='inlineStr'><is><t>8</t></is></c><c r='B3'><v>66</v></c><c r='C3'><v>85</v></c></row></sheetData></worksheet>");
        Entry(archive, "xl/worksheets/sheet2.xml", "<worksheet xmlns='http://schemas.openxmlformats.org/spreadsheetml/2006/main'><sheetData><row r='1'><c r='A1' t='inlineStr'><is><t>X</t></is></c><c r='B1' t='inlineStr'><is><t>Y</t></is></c></row><row r='2'><c r='A2'><v>84.25</v></c><c r='B2'><v>65.5</v></c></row></sheetData></worksheet>");
    }
    var xlsxPoints = PointFileService.Read(xlsx);
    Check(xlsxPoints.Count == 2 && xlsxPoints[0] == new SurveyPoint("7", 65.5, 84.25, -12.4, "Дно") && xlsxPoints[1].Height is null, "XLSX imports shared strings, inline strings, numeric cells and optional height");
    var xlsxPreview = PointImportService.Open(xlsx, "Other");
    var xlsxReview = PointImportService.Map(xlsxPreview, PointImportService.DetectMapping(xlsxPreview), hasHeader: true, swapXY: false);
    Check(xlsxPreview.Sheets.SequenceEqual(new[] { "Points", "Other" }) && xlsxReview.Points.Single() == new SurveyPoint("1", 84.25, 65.5, null, ""), "Import review selects a worksheet and previews mapped columns");
    var jsonFile = System.IO.Path.Combine(temp, "points.json");
    System.IO.File.WriteAllText(jsonFile, pointInfo, new System.Text.UTF8Encoding(false));
    Check(PointFileService.Read(jsonFile)[0].Name == "101", "JSON files route through point importer");
    var legacyPreview = PointImportService.Open(jsonFile);
    var legacyReview = PointImportService.Map(legacyPreview, PointImportService.DetectMapping(legacyPreview), hasHeader: true, swapXY: false);
    Check(legacyReview.Points.Count == 1 && legacyReview.Points[0] == new SurveyPoint("101", 65.25, 84.5, -12.4, "Карьер · Участок 2 · Пикет"), "Import review preserves legacy Khitun Geo JSON points");
    var badLegacyFile = System.IO.Path.Combine(temp, "legacy-with-bad-row.json");
    System.IO.File.WriteAllText(badLegacyFile, "{\"pointInfoArray\":[[\"Точка #: 101\",\"Точка #: 102\"],[\"Широта: 65,25 Долгота: 84,5\",\"координаты не заданы\"],[\"не задано\",\"не задано\"],[\"-12,4\",\"0\"]]}", new System.Text.UTF8Encoding(false));
    var badLegacyPreview = PointImportService.Open(badLegacyFile);
    var badLegacyReview = PointImportService.Map(badLegacyPreview, PointImportService.DetectMapping(badLegacyPreview), hasHeader: true, swapXY: false);
    Check(badLegacyReview.Points.Count == 1 && badLegacyReview.Errors.Count == 1 && badLegacyReview.InvalidRows?.SequenceEqual(new[] { 1 }) == true, "Legacy JSON preview keeps invalid entries visible instead of silently dropping them");
    var projectFile = System.IO.Path.Combine(temp, "khitun-project.json");
    System.IO.File.WriteAllText(projectFile, "{\"source\":\"wgs\",\"points\":[{\"no\":\"K-7\",\"x\":65.5,\"y\":84.25,\"z\":-12.4,\"d\":\"Дно\"}]}", new System.Text.UTF8Encoding(false));
    var projectPreview = PointImportService.Open(projectFile);
    var projectReview = PointImportService.Map(projectPreview, PointImportService.DetectMapping(projectPreview), hasHeader: true, swapXY: false);
    Check(projectReview.Points.Single() == new SurveyPoint("K-7", 65.5, 84.25, -12.4, "Дно") && projectPreview.SourceCrsId == "wgs", "Import review reads Khitun Geo project JSON and CRS metadata");
    var csvPreview = PointImportService.ParseRows("№ точки;Описание;Y;X;Z/H\nK-8;Уступ;84,5;65,5;12,25", "sample.csv");
    var csvReview = PointImportService.Map(csvPreview, PointImportService.DetectMapping(csvPreview), hasHeader: true, swapXY: false);
    Check(csvReview.Points.Single() == new SurveyPoint("K-8", 65.5, 84.5, 12.25, "Уступ"), "Import review detects reordered named columns before applying");
    var invalidReview = PointImportService.Map(csvPreview with { Rows = new[] { new[] { "№ точки", "Описание", "Y", "X", "Z/H" }, new[] { "bad", "Уступ", "84,5", "нет координаты", "12,25" } } }, PointImportService.DetectMapping(csvPreview), hasHeader: true, swapXY: false);
    Check(invalidReview.Points.Count == 0 && invalidReview.Errors.Count == 1, "Import review reports invalid rows without producing partial data");
    Check(invalidReview.InvalidRows?.SequenceEqual(new[] { 0 }) == true, "Import review identifies the invalid preview row");
    var xyzFile = System.IO.Path.Combine(temp, "survey.xyz");
    System.IO.File.WriteAllText(xyzFile, "65.5 84.5 -12.4\n66 85 -10", new System.Text.UTF8Encoding(false));
    var noHeaderXYZ = PointImportService.Open(xyzFile);
    var xyzReview = PointImportService.Map(noHeaderXYZ, PointImportService.DetectMapping(noHeaderXYZ), hasHeader: false, swapXY: false);
    Check(xyzReview.Points.Count == 2 && xyzReview.Points[0] == new SurveyPoint("1", 65.5, 84.5, -12.4, ""), "Headerless XYZ defaults to X/Y/Z coordinates");
    System.IO.File.WriteAllText(xyzFile, "65,5 84,5 -12,4", new System.Text.UTF8Encoding(false));
    var commaDecimalReview = PointImportService.Map(PointImportService.Open(xyzFile), PointImportService.DetectMapping(PointImportService.Open(xyzFile)), hasHeader: false, swapXY: false);
    Check(commaDecimalReview.Points.Single() == new SurveyPoint("1", 65.5, 84.5, -12.4, ""), "Whitespace XYZ accepts decimal commas");
    var noHeaderNamed = PointImportService.ParseRows("K1;65.5;84.5\nK2;66;85", "survey.csv", ";");
    var namedReview = PointImportService.Map(noHeaderNamed, PointImportService.DetectMapping(noHeaderNamed), hasHeader: false, swapXY: false);
    Check(namedReview.Points.Count == 2 && namedReview.Points[0] == new SurveyPoint("K1", 65.5, 84.5, null, ""), "Headerless named data defaults to name/X/Y");
    try { PointFileService.SaveCsv(file, new[] { described with { Y = double.PositiveInfinity } }); throw new Exception("Accepted infinity export"); }
    catch (ArgumentException) { Check(PointFileService.Read(file)[0] == described, "Invalid export preserves existing file"); }
    Check(System.IO.Directory.GetFiles(temp).Length == 6, "No leftover temporary files");
}
finally { System.IO.Directory.Delete(temp, true); }
Console.WriteLine("Native workspace, paste, swap and file checks passed.");
