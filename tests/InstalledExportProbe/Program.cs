using System.Reflection;
using System.Runtime.Loader;
using System.Text;

var appDirectory = Path.GetFullPath(args[0]);
AssemblyLoadContext.Default.Resolving += (_, name) =>
{
    var path = Path.Combine(appDirectory, name.Name + ".dll");
    return File.Exists(path) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(path) : null;
};
var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(appDirectory, "KhitunGeo.dll"));
if (assembly.GetName().Version != new Version(1, 7, 18, 0)) throw new Exception("Wrong installed version");
var pointType = assembly.GetType("KhitunGeo.Native.SurveyPoint", true)!;
var formatType = assembly.GetType("KhitunGeo.Native.PointExportFormat", true)!;
var optionsType = assembly.GetType("KhitunGeo.Native.PointExportOptions", true)!;
var exportType = assembly.GetType("KhitunGeo.Native.PointExport", true)!;
var points = Array.CreateInstance(pointType, 1);
points.SetValue(Activator.CreateInstance(pointType, "1", 922892.266, 55153.415, 0.0, ""), 0);
var directory = Path.Combine(Path.GetTempPath(), "khitun-installed-export-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(directory);
try
{
    foreach (var (format, expected) in new[] {
        ("Enz", "55153.415,922892.266,0.000\r\n"),
        ("AutoCad", "55153.415,922892.266,0.000\r\n"),
        ("Pnezd", "1,922892.266,55153.415,0.000,\r\n"),
        ("Penzd", "1,55153.415,922892.266,0.000,\r\n") })
    {
        var path = Path.Combine(directory, format + ".csv");
        var options = Activator.CreateInstance(optionsType, Enum.Parse(formatType, format), ",", 3, false)!;
        exportType.GetMethod("Save")!.Invoke(null, new object[] { path, points, options, false, "" });
        var bytes = File.ReadAllBytes(path);
        if (!bytes.SequenceEqual(Encoding.ASCII.GetBytes(expected)))
            throw new Exception($"Installed {format}: unexpected bytes {Convert.ToHexString(bytes)}");
        Console.WriteLine($"PASS installed 1.7.18 {format}: exact numeric bytes, no BOM, first field {expected.Split(',')[0]}");
    }
}
finally { Directory.Delete(directory, true); }
