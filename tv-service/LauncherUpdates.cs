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
 public static object Status() {lock(Gate){
  string dir=Path.Combine(Root,"updates","pending");
  if(!Directory.Exists(dir))return new{available=false,message="Kein lokales Update bereitgestellt. Der externe Paket-Installer kann ein signiertes Update hinterlegen."};
  var m=Verify(dir);return new{available=true,version=m.version,message="Geprüftes lokales Launcher-Paket verfügbar. Persönliche Webserver-Zugangsdaten bleiben erhalten."};
 }}
 public static object Install() {lock(Gate){
  string dir=Path.Combine(Root,"updates","pending");var m=Verify(dir);
  var start=new ProcessStartInfo("/usr/bin/pkgcmd"){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};
  foreach(var arg in new[]{"-i","-t","wgt","-p",Path.Combine(dir,"launcher.wgt")})start.ArgumentList.Add(arg);
  using(var p=Process.Start(start)) {
   var output=p.StandardOutput.ReadToEndAsync();var error=p.StandardError.ReadToEndAsync();
   if(!p.WaitForExit(120000)){p.Kill(true);throw new Exception("Installation hat das Zeitlimit überschritten; Status vor erneutem Versuch prüfen");}
   if(p.ExitCode!=0||!output.Result.Contains("val[ok]"))throw new Exception("Paketinstallation fehlgeschlagen; vorhandenes Paket bleibt zur Diagnose bereitgestellt");
  }
  string previous=Path.Combine(Root,"updates","last-installed");
  if(Directory.Exists(previous))Directory.Delete(previous,true);
  Directory.Move(dir,previous);
  return new{message="Launcher "+m.version+" installiert. Tizen Deck erneut öffnen."};
 }}
}
