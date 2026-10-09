using System.IO;
using System.Text.Json;
using KhitunGeo.Native;
static void Check(bool value,string name){if(!value)throw new Exception(name);}
var systems=NativeCrsCatalogue.Read(args[0]);
Check(systems.Count > 300,"Complete catalogue");
Check(systems.Select(c=>c.Id).Distinct().Count()==systems.Count,"Unique IDs");
Check(systems.Single(c=>c.Id=="wgs").Definition.GetProperty("datum").GetString()=="wgs","WGS selection");
Check(systems.Single(c=>c.Id=="gskgeo").Definition.GetProperty("datum").GetString()=="gsk2011","GSK datum");
Check(systems.Any(c=>c.Name=="МСК г. Красноярск"),"Regional updates included");
Check(systems.Any(c=>c.Definition.TryGetProperty("lon0",out var lon)&&lon.GetDouble()>180),"Chukotka longitude convention retained");
Check(!systems.Any(c=>c.Definition.GetProperty("kind").GetString() is "regional" or "custom"),"Only resolved definitions");
var invalid=Path.GetTempFileName();
try{
 foreach(var json in new[]{"{}","{\"version\":2,\"systems\":[]}","{\"version\":1,\"systems\":[{\"id\":\"a\",\"name\":\"A\",\"definition\":{\"kind\":\"regional\",\"datum\":\"sk42\"}}]}","{\"version\":1,\"systems\":[]}"}){
  File.WriteAllText(invalid,json);var rejected=false;
  try{NativeCrsCatalogue.Read(invalid);}catch(Exception ex)when(ex is FormatException or JsonException or KeyNotFoundException or InvalidOperationException){rejected=true;}
  Check(rejected,"Malformed catalogue rejected");
 }
}finally{File.Delete(invalid);}
Console.WriteLine($"PASS native catalogue: {systems.Count} definitions, datum/updates, durable JsonElements and malformed input rejection");
