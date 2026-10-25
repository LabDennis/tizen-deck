using System;using System.IO;using System.IO.Compression;using System.Text;using System.Text.Json;using System.Security.Cryptography;
class Test {
 static string root=AppContext.BaseDirectory,installed="1.6.0";static RSA rsa=RSA.Create(2048);static int installs;static bool failNext;
 static void Check(bool x,string why){if(!x)throw new Exception(why);}
 static string Slot(string s)=>Path.Combine(root,"updates",s);
 static void Package(string slot,string version,string appId="TizenDeckX.XMB"){
  var dir=Slot(slot);if(Directory.Exists(dir))Directory.Delete(dir,true);Directory.CreateDirectory(dir);
  var file=Path.Combine(dir,"launcher.wgt");using(var z=ZipFile.Open(file,ZipArchiveMode.Create))using(var s=new StreamWriter(z.CreateEntry("config.xml").Open()))s.Write("<widget xmlns='http://www.w3.org/ns/widgets' version='"+version+"'><application xmlns='http://tizen.org/ns/widgets' id='"+appId+"' package='TizenDeckX'/></widget>");
  var manifest=JsonSerializer.SerializeToUtf8Bytes(new{component="TizenDeckX.XMB",version,sha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))).ToLowerInvariant()});
  File.WriteAllBytes(Path.Combine(dir,"manifest.json"),manifest);File.WriteAllBytes(Path.Combine(dir,"manifest.sig"),rsa.SignData(manifest,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1));
 }
 static JsonElement Status()=>JsonSerializer.SerializeToElement(LauncherUpdates.Status());
 static void Refused(Action a,string why){bool refused=false;try{a();}catch{refused=true;}Check(refused,why);}
 static void Main(){
  if(Directory.Exists(Slot("")))Directory.Delete(Slot(""),true);Directory.CreateDirectory(Path.Combine(root,"state"));File.WriteAllBytes(Path.Combine(root,"state","update-public.der"),rsa.ExportSubjectPublicKeyInfo());
  LauncherUpdates.InstalledVersion=()=>installed;
  LauncherUpdates.InstallPackage=path=>{installs++;using var z=ZipFile.OpenRead(path);using var stream=z.GetEntry("config.xml").Open();installed=System.Xml.Linq.XDocument.Load(stream).Root.Attribute("version").Value;if(failNext){failNext=false;throw new Exception("Simulated failure after partial install");}};
  Check(!Status().GetProperty("available").GetBoolean(),"empty stage");
  Package("last-installed","1.6.0");Package("pending","1.7.0");Check(Status().GetProperty("available").GetBoolean(),"newer package");
  foreach(var name in new[]{"manifest.json","manifest.sig","launcher.wgt"}){
   var p=Path.Combine(Slot("pending"),name);var old=File.ReadAllBytes(p);var changed=(byte[])old.Clone();changed[changed.Length-1]^=1;File.WriteAllBytes(p,changed);Refused(()=>LauncherUpdates.Status(),"tampering accepted "+name);File.WriteAllBytes(p,old);
  }
  Package("pending","1.7.0","org.example.wrong");int beforeIdentity=installs;Refused(()=>LauncherUpdates.Install(),"signed wrong app identity refused");Check(installs==beforeIdentity,"wrong app cannot reach installer");Package("pending","1.7.0");
  failNext=true;Refused(()=>LauncherUpdates.Install(),"install failure must surface");Check(installed=="1.6.0"&&installs==2,"failed upgrade restores baseline");
  LauncherUpdates.Install();Check(installed=="1.7.0"&&Status().GetProperty("can_rollback").GetBoolean(),"successful upgrade retains rollback");
  LauncherUpdates.Rollback();Check(installed=="1.6.0","rollback version");LauncherUpdates.Rollback();Check(installed=="1.7.0","reverse rollback preserves working package");
  foreach(var v in new[]{"1.7.0","1.5.0"}){Package("pending",v);Check(!Status().GetProperty("available").GetBoolean(),"old/equal unavailable");int n=installs;Refused(()=>LauncherUpdates.Install(),"old/equal refused");Check(installs==n,"no installer called for downgrade");}
  Check(LauncherUpdates.Compare("1.10.0","1.9.0")>0,"numeric version ordering");
  Console.WriteLine("Updates: signatures/hash/identity, numeric versions, equal/downgrade refusal, partial-install recovery, retained and reversible rollback passed.");
 }
}
