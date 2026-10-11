using System.IO;
using System.Text.Json;
using KhitunGeo.Native;
static void Check(bool value,string name){if(!value)throw new Exception(name);}
var systems=NativeCrsCatalogue.Read(args[0]);
var entries=MskSelectionCatalogue.Build(systems);
var groups=entries.Where(e=>e.IsMskGroup).ToArray();
Check(groups.Length>60 && entries.Count<systems.Count,"MSK entries are grouped in main picker");
Check(groups.SelectMany(g=>g.Zones).Select(z=>z.Id).OrderBy(x=>x).SequenceEqual(systems.Where(s=>s.Id.StartsWith("msk:")).Select(s=>s.Id).OrderBy(x=>x)),"Every real catalogue zone appears exactly once, no invented zones");
Check(groups.All(g=>!g.Name.Contains("зона ")),"Main picker does not enumerate zones");
foreach(var entry in entries) foreach(var zone in entry.Zones)
 Check(zone.Definition.GetRawText()==systems.Single(s=>s.Id==zone.Id).Definition.GetRawText(),"Projection definitions preserved exactly");
Check(groups.Single(g=>g.Id=="msk-group:10").Zones.All(z=>z.Id.StartsWith("msk:10|")),"Only matching MSK region zones shown");
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
