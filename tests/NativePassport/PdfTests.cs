using System.IO;
using System.Threading;
using KhitunGeo.Native;

internal static class PdfTests
{
    internal static void Run(string regularFont, string boldFont, string directory)
    {
        Directory.CreateDirectory(directory);
        var points = new[] { new SurveyPoint("001",65,84,-12,"Глубина"), new SurveyPoint("002",65.01,84.02,null,"") };
        const string settings = "{\"version\":1,\"source\":{\"kind\":\"geo\",\"datum\":\"wgs\"},\"target\":{\"kind\":\"gk6\",\"datum\":\"sk42\"},\"zone\":{\"auto\":true,\"manual\":28,\"sourcePrefix\":true,\"targetPrefix\":true}}";
        ConversionPassport Create(string source, IReadOnlyList<QualityIssue> issues) => PassportCapture.Capture(points,source,"МСК-164",settings,"1.7.5",new DateTimeOffset(2026,10,8,14,0,0,TimeSpan.Zero)).Complete(points,new[]{15},issues);
        var plain = Create("WGS-84",Array.Empty<QualityIssue>());
        var preserved = Path.Combine(directory,"preserved.pdf"); File.WriteAllText(preserved,"original");
        try { PassportPdfWriter.Save(preserved,plain,true,CancellationToken.None); throw new Exception("Unconfigured fonts accepted"); } catch (InvalidOperationException) { }
        ModelTests.Assert(File.ReadAllText(preserved)=="original","MissingFontPreservesDestination");
        try { PassportFontResolver.Configure(Path.Combine(directory,"missing.ttf"),boldFont); throw new Exception("Missing font accepted"); } catch (FileNotFoundException) { }
        PassportFontResolver.Configure(regularFont,boldFont);
        using (var cancelled = new CancellationTokenSource())
        {
            cancelled.Cancel();
            try { PassportPdfWriter.Save(preserved,plain,true,cancelled.Token); throw new Exception("Cancelled export accepted"); } catch (OperationCanceledException) { }
        }
        ModelTests.Assert(File.ReadAllText(preserved)=="original","CancelledExportPreservesDestination");
        using (var during = new CancellationTokenSource(TimeSpan.FromMilliseconds(50)))
        {
            var huge = Create(new string('Ж',200000),Array.Empty<QualityIssue>());
            try { PassportPdfWriter.Save(preserved,huge,true,during.Token); throw new Exception("Cancellation during layout ignored"); } catch (OperationCanceledException) { }
        }
        ModelTests.Assert(File.ReadAllText(preserved)=="original","Mid-layout cancellation preserves destination");
        var inaccessible = Path.Combine(directory,"destination-directory"); Directory.CreateDirectory(inaccessible);
        try { PassportPdfWriter.Save(inaccessible,plain,true,CancellationToken.None); throw new Exception("Directory destination accepted"); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
        ModelTests.Assert(Directory.Exists(inaccessible),"Failed export preserves destination directory");
        ModelTests.Assert(Directory.GetFiles(directory,"*.tmp").Length==0,"Failed export cleans temporary PDF");
        var unsupported = Create("Недоступный символ \u4e00",Array.Empty<QualityIssue>());
        try { PassportPdfWriter.Save(preserved,unsupported,true,CancellationToken.None); throw new Exception("Missing glyph accepted"); } catch (InvalidOperationException) { }
        ModelTests.Assert(File.ReadAllText(preserved)=="original","Missing glyph preserves existing PDF");
        PassportPdfWriter.Save(Path.Combine(directory,"short.pdf"),plain,true,CancellationToken.None);
        PassportPdfWriter.Save(Path.Combine(directory,"long.pdf"),Create("Длинная система " + new string('Ж',1500),Array.Empty<QualityIssue>()),false,CancellationToken.None);
        var many = Enumerable.Range(0,700).Select(i=>new QualityIssue(i%2,"warning",$"Замечание {i:D4}: высота не задана; координаты и номер проверены.")).ToArray();
        PassportPdfWriter.Save(Path.Combine(directory,"many.pdf"),Create("WGS-84",many),true,CancellationToken.None);
        ModelTests.Assert(Directory.GetFiles(directory,"*.tmp").Length==0,"Temporary PDF leaked");
        Console.WriteLine("PASS real PDF creation, cancellation, glyph/font failure, file preservation and pagination");
    }
}
