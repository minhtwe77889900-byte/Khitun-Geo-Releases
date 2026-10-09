ModelTests.Run();
#if QUALITY
QualityTests.Run();
#endif
#if WORKFLOW
PassportWorkflowTests.Run();
#endif
#if PDF
if (args.Length == 3) PdfTests.Run(args[0], args[1], args[2]);
else
{
    var fonts = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
    var temporary = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "khitun-passport-test-" + Guid.NewGuid().ToString("N"));
    try { PdfTests.Run(System.IO.Path.Combine(fonts,"arial.ttf"),System.IO.Path.Combine(fonts,"arialbd.ttf"),temporary); }
    finally { if (System.IO.Directory.Exists(temporary)) System.IO.Directory.Delete(temporary,true); }
}
#endif
