using System;using System.IO;using System.Net.Http;using System.Threading;using System.Text.Json;using System.Threading.Tasks;using System.Security.Cryptography;using System.Security.Cryptography.X509Certificates;using System.Net.Security;using System.Text.RegularExpressions;

// User-selected source, manual checks only. Local sources can pin their TLS identity.
internal static class ReleaseFeed {
 internal sealed class Config {public string url{get;set;}="";public string certificate_sha256{get;set;}="";public string access_token{get;set;}="";}
 static string ConfigFile=>Path.Combine(BackendUpdates.Root,"state/release-feed.json");
 internal static string Validate(string value){
  if(string.IsNullOrWhiteSpace(value))return "";
  if(value.Length>1024||!Uri.TryCreate(value,UriKind.Absolute,out var uri)||uri.Scheme!="https"||uri.UserInfo!=""||uri.Query!=""||uri.Fragment!="")throw new Exception("HTTPS-Adresse ohne Zugangsdaten, Query oder Fragment verwenden");
  return uri.AbsoluteUri.TrimEnd('/')+"/";
 }
 static Config Read()=>File.Exists(ConfigFile)?JsonSerializer.Deserialize<Config>(File.ReadAllText(ConfigFile)):new Config();
 internal static object Status(){var c=Read();return new{url=Validate(c.url),configured=c.url!="",automatic=false,pinned=c.certificate_sha256!="",authenticated=c.access_token!=""};}
 internal static object Save(string value,string fingerprint=null,string token=null){lock(BackendUpdates.Gate){
  var c=Read();string url=Validate(value);
  // Never carry a credential or TLS exception across a destination change.
  if(url!=c.url){c.certificate_sha256="";c.access_token="";}c.url=url;
  if(fingerprint!=null){if(fingerprint!=""&&!Regex.IsMatch(fingerprint,"^[a-fA-F0-9]{64}$"))throw new Exception("Ungültiger Zertifikat-Fingerabdruck");c.certificate_sha256=fingerprint.ToLowerInvariant();}
  if(token!=null){if(token!=""&&!Regex.IsMatch(token,"^[A-Za-z0-9_-]{32,128}$"))throw new Exception("Ungültiger Release-Lesezugang");c.access_token=token;}
  File.WriteAllText(ConfigFile+".new",JsonSerializer.Serialize(c));File.Move(ConfigFile+".new",ConfigFile,true);return Status();
 }}
 internal static bool Trust(X509Certificate2 cert,SslPolicyErrors errors,string pin){
  if(pin=="")return errors==SslPolicyErrors.None;
  if(cert==null||DateTime.UtcNow<cert.NotBefore.ToUniversalTime()||DateTime.UtcNow>cert.NotAfter.ToUniversalTime())return false;
  using(var sha=SHA256.Create()){var hash=BitConverter.ToString(sha.ComputeHash(cert.RawData)).Replace("-","").ToLowerInvariant();return hash==pin;}
 }
 internal static async Task Download(HttpClient client,Uri uri,string path,int limit,CancellationToken token){
  using(var response=await client.GetAsync(uri,HttpCompletionOption.ResponseHeadersRead,token)){
   if((int)response.StatusCode!=200)throw new Exception("Release-Datei nicht verfügbar (HTTP "+(int)response.StatusCode+")");
   if(response.Content.Headers.ContentLength>limit)throw new Exception("Release-Datei zu groß");
   using(var input=await response.Content.ReadAsStreamAsync())using(var output=new FileStream(path,FileMode.CreateNew)){
    byte[] buffer=new byte[16384];int read,total=0;while((read=await input.ReadAsync(buffer,0,buffer.Length,token))>0){if((total+=read)>limit)throw new Exception("Release-Datei zu groß");output.Write(buffer,0,read);}
   }
  }
 }
 internal static object Check(string component="backend"){lock(BackendUpdates.Gate){
  if(component!="backend"&&component!="launcher")throw new Exception("Unbekannte Update-Komponente");
  var c=Read();string url=Validate(c.url);if(url=="")throw new Exception("Zuerst eine eigene Release-Adresse speichern");
  var depot=BackendUpdates.Depot;Directory.CreateDirectory(depot);var incoming=Path.Combine(depot,"download-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(incoming);
  string pending=Path.Combine(depot,"pending"),old=Path.Combine(depot,"stage-"+Guid.NewGuid().ToString("N"));
  try{
   using(var stop=new CancellationTokenSource(TimeSpan.FromSeconds(25)))using(var handler=new HttpClientHandler{AllowAutoRedirect=false,UseProxy=false}){
    if(c.certificate_sha256!="")handler.ServerCertificateCustomValidationCallback=(request,cert,chain,errors)=>Trust(cert,errors,c.certificate_sha256);
    using(var client=new HttpClient(handler)){
     if(c.access_token!="")client.DefaultRequestHeaders.Authorization=new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",c.access_token);
     var root=new Uri(new Uri(url),component+"/");
     Download(client,new Uri(root,"manifest.json"),Path.Combine(incoming,"manifest.json"),4096,stop.Token).GetAwaiter().GetResult();
     Download(client,new Uri(root,"manifest.sig"),Path.Combine(incoming,"manifest.sig"),1024,stop.Token).GetAwaiter().GetResult();
     var file=component=="backend"?"backend.zip":"launcher.wgt";
     Download(client,new Uri(root,file),Path.Combine(incoming,file),16777216,stop.Token).GetAwaiter().GetResult();
    }
   }
   if(component=="launcher")return LauncherUpdates.Stage(incoming);
   var manifest=BackendUpdates.Verify(incoming,Path.Combine(BackendUpdates.Root,"state/update-public.der"),BackendUpdates.CurrentFirmware());
   BackendUpdates.CheckAssemblyAfterExtraction(incoming,manifest.version);
   if(Directory.Exists(pending))Directory.Move(pending,old);
   try{Directory.Move(incoming,pending);}catch{if(Directory.Exists(old))Directory.Move(old,pending);throw;}
   if(Directory.Exists(old))Directory.Delete(old,true);
   return new{message="Signiertes Backend-Paket "+manifest.version+" bereit. Installation separat bestätigen.",version=manifest.version};
  }finally{if(Directory.Exists(incoming))Directory.Delete(incoming,true);}
 }}
}
