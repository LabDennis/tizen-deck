using System;using System.IO;using System.IO.Compression;using System.Text.Json;using System.Security.Cryptography;using System.Reflection;using System.Linq;using System.Net;using System.Net.Http;using System.Threading;using System.Threading.Tasks;
static class ServiceHealth {internal static string Version=>"1.8.0";}
class Test {
 static RSA rsa=RSA.Create(2048);static string root=Path.Combine(Path.GetTempPath(),"deck-backend-test-"+Guid.NewGuid().ToString("N")),slot,key;
 static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
 static void Refused(Action a,string why){bool no=false;try{a();}catch{no=true;}Check(no,why);}
 static void Sign(string component="DeckTV",string version="1.8.0",string fw="T-NKLBWWC-1720.7"){
  var m=JsonSerializer.SerializeToUtf8Bytes(new{component,version,firmware=fw,sha256=BackendUpdates.Hash(Path.Combine(slot,"backend.zip"))});File.WriteAllBytes(Path.Combine(slot,"manifest.json"),m);File.WriteAllBytes(Path.Combine(slot,"manifest.sig"),rsa.SignData(m,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1));
 }
 static void Package(string alternate=null,bool link=false,bool duplicate=false){
  File.Delete(Path.Combine(slot,"backend.zip"));using(var z=ZipFile.Open(Path.Combine(slot,"backend.zip"),ZipArchiveMode.Create))foreach(var n in BackendUpdates.Files){var e=z.CreateEntry(n=="static/app.js"?(alternate??n):n);if(link&&n=="static/app.js")e.ExternalAttributes=0xA000<<16;using(var s=e.Open()){var data=n=="DeckTV.dll"?File.ReadAllBytes(Assembly.GetExecutingAssembly().Location):new byte[]{1,2,3};s.Write(data);}if(duplicate&&n=="static/app.js")z.CreateEntry(n); }Sign();
 }
 sealed class FakeHandler:HttpMessageHandler{public HttpStatusCode Code=HttpStatusCode.OK;public byte[] Data=new byte[]{1,2,3};protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token){return Task.FromResult(new HttpResponseMessage(Code){Content=new ByteArrayContent(Data)});}}
 static void Downloads(){
  var handler=new FakeHandler();using(var client=new HttpClient(handler)){var p=Path.Combine(root,"download");ReleaseFeed.Download(client,new Uri("https://example.test/file"),p,5,CancellationToken.None).GetAwaiter().GetResult();Check(File.ReadAllBytes(p).Length==3,"bounded download");File.Delete(p);
  handler.Code=HttpStatusCode.Redirect;Refused(()=>ReleaseFeed.Download(client,new Uri("https://example.test/file"),p,5,CancellationToken.None).GetAwaiter().GetResult(),"redirect accepted");Check(!File.Exists(p),"redirect must not write file");
  handler.Code=HttpStatusCode.OK;handler.Data=new byte[10];Refused(()=>ReleaseFeed.Download(client,new Uri("https://example.test/file"),p,5,CancellationToken.None).GetAwaiter().GetResult(),"oversize download");}
 }
 static void Main(){Directory.CreateDirectory(root);slot=Path.Combine(root,"pending");Directory.CreateDirectory(slot);key=Path.Combine(root,"public.der");File.WriteAllBytes(key,rsa.ExportSubjectPublicKeyInfo());try{
  Downloads();Check(ReleaseFeed.Validate("https://example.test/releases")=="https://example.test/releases/","feed normalized");Check(ReleaseFeed.Validate("")=="","empty feed disabled");foreach(var bad in new[]{"http://example.test/","https://user:pass@example.test/","https://example.test/?token=x","https://example.test/#secret","file:///etc/passwd"})Refused(()=>ReleaseFeed.Validate(bad),"invalid feed accepted");
  Package();Check(BackendUpdates.Verify(slot,key,"T-NKLBWWC-1720.7").version=="1.8.0","valid signed release");
  foreach(var file in new[]{"manifest.json","manifest.sig","backend.zip"}){var p=Path.Combine(slot,file);var b=File.ReadAllBytes(p);var c=(byte[])b.Clone();c[c.Length-1]^=1;File.WriteAllBytes(p,c);Refused(()=>BackendUpdates.Verify(slot,key,"T-NKLBWWC-1720.7"),"tampered "+file);File.WriteAllBytes(p,b);}
  Sign("wrong");Refused(()=>BackendUpdates.Verify(slot,key,"T-NKLBWWC-1720.7"),"component mismatch");Sign(version:"1.8.0;echo");Refused(()=>BackendUpdates.Verify(slot,key,"T-NKLBWWC-1720.7"),"invalid version");Sign();Refused(()=>BackendUpdates.Verify(slot,key,"T-NKLBWWC-9999.0"),"other firmware");
  foreach(var path in new[]{"../escape","/tmp/escape","static/../../escape","state/auth.json"}){Package(path);Refused(()=>BackendUpdates.Verify(slot,key,"T-NKLBWWC-1720.7"),"path accepted: "+path);Refused(()=>BackendUpdates.Extract(Path.Combine(slot,"backend.zip"),Path.Combine(root,"bad")),"extract must independently validate");Check(!Directory.Exists(Path.Combine(root,"bad")),"invalid extract creates nothing");}
  Package(link:true);Refused(()=>BackendUpdates.Verify(slot,key,"T-NKLBWWC-1720.7"),"symlink accepted");Package(duplicate:true);Refused(()=>BackendUpdates.Verify(slot,key,"T-NKLBWWC-1720.7"),"duplicate accepted");
  Package();var dest=Path.Combine(root,"valid");BackendUpdates.Extract(Path.Combine(slot,"backend.zip"),dest);BackendUpdates.CheckAssembly(dest,"1.8.0");Refused(()=>BackendUpdates.CheckAssembly(dest,"1.9.0"),"assembly mismatch");Refused(()=>BackendUpdates.Extract(Path.Combine(slot,"backend.zip"),dest),"existing destination overwrite");
  Check(BackendUpdates.Worker("/next","/backup","/job","1.8.0").Contains("BACKUP=/backup"),"embedded worker");
  Console.WriteLine("Backend: valid signature/hash, tamper rejection, firmware/identity, unsafe archive paths/types, duplicate entries and DLL version passed.");
 }finally{Directory.Delete(root,true);}}
}
