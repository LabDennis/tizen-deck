using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using System.Linq;
using System.Diagnostics;

// Updates are deliberately limited to our launcher. No firmware or shell payloads.
static class LauncherUpdates {
 static readonly object Gate=new object();
 static string Root=AppContext.BaseDirectory;
 class Manifest {public string component{get;set;} public string version{get;set;} public string sha256{get;set;}}
 static Manifest Verify(string dir) {
  var manifest=File.ReadAllBytes(Path.Combine(dir,"manifest.json"));
  if(manifest.Length>4096)throw new Exception("Update-Manifest zu groß");
  using(var rsa=RSA.Create()) {
   var key=File.ReadAllBytes(Path.Combine(Root,"state","update-public.der"));
   rsa.ImportSubjectPublicKeyInfo(key,out _);
   if(!rsa.VerifyData(manifest,File.ReadAllBytes(Path.Combine(dir,"manifest.sig")),HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1))throw new Exception("Update-Signatur ungültig");
  }
  var m=JsonSerializer.Deserialize<Manifest>(manifest);
  if(m.component!="TizenDeckX.XMB"||!System.Text.RegularExpressions.Regex.IsMatch(m.version??"","^[0-9]{1,4}\\.[0-9]{1,4}\\.[0-9]{1,4}$"))throw new Exception("Unbekanntes Update-Ziel");
  string path=Path.Combine(dir,"launcher.wgt");
  var size=new FileInfo(path).Length;if(size<100||size>33554432)throw new Exception("Ungültige Paketgröße");
  using(var sha=SHA256.Create())using(var f=File.OpenRead(path)) {
   var hash=BitConverter.ToString(sha.ComputeHash(f)).Replace("-","").ToLowerInvariant();
   if(hash!=m.sha256)throw new Exception("Paket-Prüfsumme stimmt nicht");
  }
  using(var zip=ZipFile.OpenRead(path)) {
   var entries=zip.Entries.Where(e=>e.FullName=="config.xml").ToArray();
   if(entries.Length!=1||entries[0].Length>65536)throw new Exception("Ungültige Paketbeschreibung");
   using(var stream=entries[0].Open())using(var reader=XmlReader.Create(stream,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=65536})) {
    var doc=XDocument.Load(reader);var app=doc.Descendants(XName.Get("application","http://tizen.org/ns/widgets")).Single();
    if((string)app.Attribute("id")!="TizenDeckX.XMB"||(string)app.Attribute("package")!="TizenDeckX"||(string)doc.Root.Attribute("version")!=m.version)throw new Exception("Paketidentität stimmt nicht");
   }
  }
  return m;
 }
 internal static Func<string> InstalledVersion = ReadInstalled;
 internal static Action<string> InstallPackage = RunInstaller;
 static string ReadInstalled() {
  var start=new ProcessStartInfo("/usr/bin/sqlite3"){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};
  start.ArgumentList.Add("/opt/dbspace/.pkgmgr_parser.db");
  start.ArgumentList.Add("select package_version from package_info where package='TizenDeckX';");
  using(var p=Process.Start(start)) {
   var output=p.StandardOutput.ReadToEndAsync();var error=p.StandardError.ReadToEndAsync();
   if(!p.WaitForExit(3000)){p.Kill(true);throw new Exception("Paketstatus antwortet nicht");}
   string version=output.Result.Trim();
   if(p.ExitCode!=0||!Version.TryParse(version,out _))throw new Exception("Installierte Launcher-Version unbekannt");
   return version;
  }
 }
 static void RunInstaller(string path) {
  var start=new ProcessStartInfo("/usr/bin/pkgcmd"){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};
  foreach(var arg in new[]{"-i","-t","wgt","-p",path})start.ArgumentList.Add(arg);
  using(var p=Process.Start(start)) {
   var output=p.StandardOutput.ReadToEndAsync();var error=p.StandardError.ReadToEndAsync();
   if(!p.WaitForExit(120000)){p.Kill(true);throw new Exception("Installation hat das Zeitlimit überschritten");}
   if(p.ExitCode!=0||!output.Result.Contains("val[ok]"))throw new Exception("Paketinstallation fehlgeschlagen");
  }
 }
 static string Slot(string name){return Path.Combine(Root,"updates",name);}
 static bool Exists(string name){return Directory.Exists(Slot(name));}
 internal static int Compare(string next,string current){return Version.Parse(next).CompareTo(Version.Parse(current));}
 public static object Status() {lock(Gate){
  var installed=InstalledVersion();var pending=Exists("pending")?Verify(Slot("pending")):null;
  var rollback=Exists("rollback")?Verify(Slot("rollback")):null;
  bool available=pending!=null&&Compare(pending.version,installed)>0;
  return new{available,installed,version=pending?.version,rollback_version=rollback?.version,
   can_rollback=rollback!=null&&Compare(rollback.version,installed)!=0,
   message=pending==null?"Kein lokales Update bereitgestellt.":available?"Signiertes Launcher-Update bereit. Die vorherige gesicherte Version bleibt als Rückfall erhalten.":"Das bereitgestellte Paket ist gleich alt oder älter. Kein normales Update möglich."};
 }}
 internal static object Stage(string incoming){lock(Gate){
  var manifest=Verify(incoming);Directory.CreateDirectory(Path.Combine(Root,"updates"));
  var pending=Slot("pending");var old=Slot("stage-"+Guid.NewGuid().ToString("N"));
  if(Directory.Exists(pending))Directory.Move(pending,old);
  try{Directory.Move(incoming,pending);}catch{if(Directory.Exists(old))Directory.Move(old,pending);throw;}
  if(Directory.Exists(old))Directory.Delete(old,true);
  return new{message="Signiertes Launcher-Paket "+manifest.version+" bereit. Installation separat bestätigen.",version=manifest.version};
 }}
 public static object Install() {lock(Gate){
  var installed=InstalledVersion();var m=Verify(Slot("pending"));
  if(Compare(m.version,installed)<=0)throw new Exception("Normale Updates müssen neuer sein; für Rückfälle die Wiederherstellung verwenden");
  bool backup=Exists("last-installed")&&Verify(Slot("last-installed")).version==installed;
  try {
   InstallPackage(Path.Combine(Slot("pending"),"launcher.wgt"));
   if(InstalledVersion()!=m.version)throw new Exception("Versionsprüfung nach Installation fehlgeschlagen");
  } catch(Exception failure) {
   if(backup) {
    try {InstallPackage(Path.Combine(Slot("last-installed"),"launcher.wgt"));if(InstalledVersion()!=installed)throw new Exception("Versionsprüfung fehlgeschlagen");}
    catch(Exception restore){throw new Exception("Update und Rückfall fehlgeschlagen: "+failure.Message+" / "+restore.Message);}
    throw new Exception("Update fehlgeschlagen; Version "+installed+" wiederhergestellt: "+failure.Message);
   }
   throw new Exception("Update fehlgeschlagen; kein passendes Rückfallpaket gesichert. Paketstatus prüfen: "+failure.Message);
  }
  if(backup) {
   if(Exists("rollback"))Directory.Delete(Slot("rollback"),true);
   Directory.Move(Slot("last-installed"),Slot("rollback"));
  } else if(Exists("last-installed"))Directory.Delete(Slot("last-installed"),true);
  Directory.Move(Slot("pending"),Slot("last-installed"));
  return new{message="Launcher "+m.version+" installiert und Paketversion geprüft. Tizen Deck erneut öffnen."};
 }}
 public static object Rollback() {lock(Gate){
  var m=Verify(Slot("rollback"));var installed=InstalledVersion();
  if(Compare(m.version,installed)==0)throw new Exception("Rückfallversion bereits installiert");
  InstallPackage(Path.Combine(Slot("rollback"),"launcher.wgt"));
  if(InstalledVersion()!=m.version)throw new Exception("Rückfall nicht bestätigt; Pakete bleiben erhalten");
  // Preserve the package being rolled back from, if it matches the previous installation.
  if(Exists("last-installed")&&Verify(Slot("last-installed")).version==installed) {
   string swap=Slot("swap-"+Guid.NewGuid().ToString("N"));
   Directory.Move(Slot("last-installed"),swap);Directory.Move(Slot("rollback"),Slot("last-installed"));Directory.Move(swap,Slot("rollback"));
  } else {
   if(Exists("last-installed"))Directory.Delete(Slot("last-installed"),true);
   Directory.Move(Slot("rollback"),Slot("last-installed"));
  }
  return new{message="Launcher "+m.version+" wiederhergestellt und Paketversion geprüft."};
 }}
}
