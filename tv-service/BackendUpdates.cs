using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Reflection;
using System.Text.Json;
using System.Collections.Generic;
using System.Diagnostics;

// Administrative updates to Deck only. No firmware, boot hooks or vendor files.
internal static class BackendUpdates {
 internal const string Component="DeckTV";
 internal static string Root=AppContext.BaseDirectory.TrimEnd('/');
 internal static string Depot="/opt/usr/tizen-deck-updates";
 internal static readonly object Gate=new object();
 internal static readonly string[] Files={"DeckTV.dll","DeckTV.deps.json","DeckTV.runtimeconfig.json","deck-tv.service","static/index.html","static/app.js","static/input.js","static/style.css"};
 internal sealed class Manifest {public string component{get;set;}public string version{get;set;}public string sha256{get;set;}public string firmware{get;set;}}
 internal sealed class Result {public string phase{get;set;}public string message{get;set;}public string version{get;set;}}
 internal static string Hash(string path){using(var sha=SHA256.Create())using(var f=File.OpenRead(path))return BitConverter.ToString(sha.ComputeHash(f)).Replace("-","").ToLowerInvariant();}
 internal static bool ValidVersion(string v)=>System.Text.RegularExpressions.Regex.IsMatch(v??"","^[0-9]{1,4}\\.[0-9]{1,4}\\.[0-9]{1,4}$");
 internal static Manifest Verify(string directory,string keyPath,string firmware){
  string m=Path.Combine(directory,"manifest.json"),sig=Path.Combine(directory,"manifest.sig"),zip=Path.Combine(directory,"backend.zip");
  if(new FileInfo(m).Length>4096||new FileInfo(sig).Length>1024)throw new Exception("Update-Metadaten zu groß");
  byte[] data=File.ReadAllBytes(m);using(var rsa=RSA.Create()){rsa.ImportSubjectPublicKeyInfo(File.ReadAllBytes(keyPath),out _);if(!rsa.VerifyData(data,File.ReadAllBytes(sig),HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1))throw new Exception("Backend-Signatur ungültig");}
  var manifest=JsonSerializer.Deserialize<Manifest>(data);
  if(manifest==null||manifest.component!=Component||!ValidVersion(manifest.version)||manifest.firmware!="T-NKLBWWC-1720.7"||!firmware.Contains(manifest.firmware))throw new Exception("Backend-Ziel oder Firmware passt nicht");
  if(new FileInfo(zip).Length>16777216||Hash(zip)!=manifest.sha256)throw new Exception("Backend-Paketprüfung fehlgeschlagen");
  ValidateArchive(zip);
  return manifest;
 }
 internal static void ValidateArchive(string zip){
  using(var z=ZipFile.OpenRead(zip)){
   if(z.Entries.Count!=Files.Length)throw new Exception("Unerwarteter Paketinhalt");
   long total=0;var seen=new HashSet<string>();foreach(var e in z.Entries){
    int type=(e.ExternalAttributes>>16)&0xF000;
    if(!Files.Contains(e.FullName)||!seen.Add(e.FullName)||(type!=0&&type!=0x8000)||e.Length>8388608||(total+=e.Length)>33554432)throw new Exception("Ungültiger Paketpfad oder Dateityp");
   }
  }
 }
 internal static void Extract(string package,string destination){
  ValidateArchive(package);if(Directory.Exists(destination))throw new Exception("Ziel existiert bereits");Directory.CreateDirectory(destination);
  using(var zip=ZipFile.OpenRead(package))foreach(var e in zip.Entries){string file=Path.Combine(destination,e.FullName);Directory.CreateDirectory(Path.GetDirectoryName(file));using(var input=e.Open())using(var output=new FileStream(file,FileMode.CreateNew))input.CopyTo(output);}
 }
 internal static void CheckAssemblyAfterExtraction(string directory,string version){var path=Path.Combine(directory,"verified");try{Extract(Path.Combine(directory,"backend.zip"),path);CheckAssembly(path,version);}finally{if(Directory.Exists(path))Directory.Delete(path,true);}}
 internal static void CheckAssembly(string directory,string expected){var v=AssemblyName.GetAssemblyName(Path.Combine(directory,"DeckTV.dll")).Version;if(v.Major+"."+v.Minor+"."+v.Build!=expected)throw new Exception("DLL-Version stimmt nicht mit Manifest überein");}
 internal static string CurrentFirmware()=>File.ReadAllText("/etc/prd_info.ini");
 static string Previous=>Path.Combine(Depot,"previous.json");
 static string State=>Path.Combine(Depot,"result.json");
 static Result ReadResult(){if(!File.Exists(State))return null;return JsonSerializer.Deserialize<Result>(File.ReadAllText(State))??throw new Exception("Updatezustand unlesbar; Sicherung prüfen");}
 static bool Busy()=>ReadResult()?.phase=="running";
 internal static void WriteResult(Result result){File.WriteAllText(State+".new",JsonSerializer.Serialize(result));File.Move(State+".new",State,true);}
 internal sealed class Backup {public string path{get;set;}public string version{get;set;}public Dictionary<string,string> hashes{get;set;}}
 static Backup ReadBackup(){
  if(!File.Exists(Previous))return null;var b=JsonSerializer.Deserialize<Backup>(File.ReadAllText(Previous));
  if(b==null||!ValidVersion(b.version)||!System.Text.RegularExpressions.Regex.IsMatch(b.path??"","^"+System.Text.RegularExpressions.Regex.Escape(Depot)+"/backup-[a-f0-9]{32}$")||b.hashes==null||b.hashes.Count!=Files.Length||Files.Any(f=>!b.hashes.ContainsKey(f)))throw new Exception("Ungültige Rückfallsicherung");
  foreach(var f in Files)if(Hash(Path.Combine(b.path,f))!=b.hashes[f])throw new Exception("Rückfallsicherung wurde verändert");
  CheckAssembly(b.path,b.version);return b;
 }
 internal static object Status(){lock(Gate){
  Manifest pending=null;string error="";try{if(Directory.Exists(Path.Combine(Depot,"pending")))pending=Verify(Path.Combine(Depot,"pending"),Path.Combine(Root,"state/update-public.der"),CurrentFirmware());}catch(Exception e){error=e.Message;}
  Backup backup=null;try{backup=ReadBackup();}catch(Exception e){error=e.Message;}
  Result result=null;bool busy=false;try{result=ReadResult();busy=result?.phase=="running";}catch(Exception e){error=e.Message;busy=true;}
  return new{installed=ServiceHealth.Version,available=pending!=null&&Version.Parse(pending.version)>Version.Parse(ServiceHealth.Version),version=pending?.version,can_rollback=backup!=null&&backup.version!=ServiceHealth.Version,rollback_version=backup?.version,busy,result,error};
 }}
 internal static object Queue(bool rollback){lock(Gate){
  if(Busy())throw new Exception("Ein Backend-Update läuft bereits");
  Directory.CreateDirectory(Depot);Run("/bin/chmod",new[]{"700",Depot});
  string id=Guid.NewGuid().ToString("N"),next=Path.Combine(Depot,"next-"+id),backupPath=Path.Combine(Depot,"backup-"+id),expected;
  if(rollback){var old=ReadBackup()??throw new Exception("Keine Rückfallsicherung");expected=old.version;if(expected==ServiceHealth.Version)throw new Exception("Rückfallversion bereits aktiv");Directory.CreateDirectory(next);foreach(var f in Files){var target=Path.Combine(next,f);Directory.CreateDirectory(Path.GetDirectoryName(target));File.Copy(Path.Combine(old.path,f),target);}}
  else{var m=Verify(Path.Combine(Depot,"pending"),Path.Combine(Root,"state/update-public.der"),CurrentFirmware());expected=m.version;if(Version.Parse(expected)<=Version.Parse(ServiceHealth.Version))throw new Exception("Normales Backend-Update muss neuer sein");Extract(Path.Combine(Depot,"pending/backend.zip"),next);}
  CheckAssembly(next,expected);
  var saved=new Backup{path=backupPath,version=ServiceHealth.Version,hashes=Files.ToDictionary(f=>f,f=>Hash(Path.Combine(Root,f)))};
  string job=Path.Combine(Depot,"job-"+id);Directory.CreateDirectory(job);File.WriteAllText(Path.Combine(job,"previous.json"),JsonSerializer.Serialize(saved));
  WriteResult(new Result{phase="running",version=expected,message="Backend-Wechsel vorbereitet"});
  File.WriteAllText(Path.Combine(job,"worker.sh"),Worker(next,backupPath,job,expected));
  try{Run("/usr/bin/systemd-run",new[]{"--unit=deck-update-"+id,"--on-active=2s","/bin/sh","-c","cat "+job+"/worker.sh | /bin/sh"});}
  catch{WriteResult(new Result{phase="failed",version=expected,message="Update konnte nicht gestartet werden"});throw;}
  return new{message="Backend-Wechsel eingeplant. Verbindung wird kurz unterbrochen; anschließend Status prüfen.",version=expected};
 }}
 internal static string Worker(string next,string backup,string job,string version){
  // Every interpolated path is generated locally; version already matches a numeric grammar.
  using(var stream=typeof(BackendUpdates).Assembly.GetManifestResourceStream("DeckTV.backend-update.sh"))
  using(var reader=new StreamReader(stream??throw new Exception("Update-Worker fehlt")))
   return reader.ReadToEnd().Replace("NEXT_PATH",next).Replace("BACKUP_PATH",backup).Replace("JOB_PATH",job).Replace("NEW_VERSION",version).Replace("OLD_VERSION",ServiceHealth.Version);
 }
 static void Run(string exe,string[] args){var si=new ProcessStartInfo(exe){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};foreach(var a in args)si.ArgumentList.Add(a);using(var p=Process.Start(si)){var a=p.StandardOutput.ReadToEndAsync();var b=p.StandardError.ReadToEndAsync();if(!p.WaitForExit(5000)){p.Kill(true);throw new Exception("Dienstverwaltung antwortet nicht");}if(p.ExitCode!=0)throw new Exception("Dienstverwaltung fehlgeschlagen: "+b.Result);}}
}
