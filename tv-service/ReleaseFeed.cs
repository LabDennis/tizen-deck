using System;using System.IO;using System.Net.Http;using System.Threading;using System.Text.Json;using System.Threading.Tasks;

// Explicit, user-configured HTTPS feed. No polling, credentials or built-in endpoint.
internal static class ReleaseFeed {
 internal sealed class Config {public string url{get;set;}="";}
 static string ConfigFile=>Path.Combine(BackendUpdates.Root,"state/release-feed.json");
 internal static string Validate(string value){
  if(string.IsNullOrWhiteSpace(value))return "";
  if(value.Length>1024||!Uri.TryCreate(value,UriKind.Absolute,out var uri)||uri.Scheme!="https"||uri.UserInfo!=""||uri.Query!=""||uri.Fragment!="")throw new Exception("HTTPS-Adresse ohne Zugangsdaten, Query oder Fragment verwenden");
  return uri.AbsoluteUri.TrimEnd('/')+"/";
 }
 internal static object Status(){var url=File.Exists(ConfigFile)?JsonSerializer.Deserialize<Config>(File.ReadAllText(ConfigFile)).url:"";return new{url=Validate(url),configured=url!="",automatic=false};}
 static string Url()=>File.Exists(ConfigFile)?Validate(JsonSerializer.Deserialize<Config>(File.ReadAllText(ConfigFile)).url):"";
 internal static object Save(string value){lock(BackendUpdates.Gate){string url=Validate(value);File.WriteAllText(ConfigFile+".new",JsonSerializer.Serialize(new Config{url=url}));File.Move(ConfigFile+".new",ConfigFile,true);return Status();}}
 internal static async Task Download(HttpClient client,Uri uri,string path,int limit,CancellationToken token){
  using(var response=await client.GetAsync(uri,HttpCompletionOption.ResponseHeadersRead,token)){
   if((int)response.StatusCode!=200)throw new Exception("Release-Datei nicht verfügbar (HTTP "+(int)response.StatusCode+")");
   if(response.Content.Headers.ContentLength>limit)throw new Exception("Release-Datei zu groß");
   using(var input=await response.Content.ReadAsStreamAsync())using(var output=new FileStream(path,FileMode.CreateNew)){
    byte[] buffer=new byte[16384];int read,total=0;while((read=await input.ReadAsync(buffer,0,buffer.Length,token))>0){if((total+=read)>limit)throw new Exception("Release-Datei zu groß");output.Write(buffer,0,read);}
   }
  }
 }
 internal static object Check(){lock(BackendUpdates.Gate){
  string url=Url();if(url=="")throw new Exception("Zuerst eine eigene Release-Adresse speichern");
  var depot=BackendUpdates.Depot;Directory.CreateDirectory(depot);var incoming=Path.Combine(depot,"download-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(incoming);
  string pending=Path.Combine(depot,"pending"),old=Path.Combine(depot,"stage-"+Guid.NewGuid().ToString("N"));
  try{
   using(var stop=new CancellationTokenSource(TimeSpan.FromSeconds(25)))using(var handler=new HttpClientHandler{AllowAutoRedirect=false,UseProxy=false})using(var client=new HttpClient(handler)){
    var root=new Uri(url);Download(client,new Uri(root,"manifest.json"),Path.Combine(incoming,"manifest.json"),4096,stop.Token).GetAwaiter().GetResult();
    Download(client,new Uri(root,"manifest.sig"),Path.Combine(incoming,"manifest.sig"),1024,stop.Token).GetAwaiter().GetResult();
    Download(client,new Uri(root,"backend.zip"),Path.Combine(incoming,"backend.zip"),16777216,stop.Token).GetAwaiter().GetResult();
   }
   var manifest=BackendUpdates.Verify(incoming,Path.Combine(BackendUpdates.Root,"state/update-public.der"),BackendUpdates.CurrentFirmware());
   BackendUpdates.CheckAssemblyAfterExtraction(incoming,manifest.version);
   if(Directory.Exists(pending))Directory.Move(pending,old);
   try{Directory.Move(incoming,pending);}catch{if(Directory.Exists(old))Directory.Move(old,pending);throw;}
   if(Directory.Exists(old))Directory.Delete(old,true);
   return new{message="Signiertes Backend-Paket "+manifest.version+" bereit. Installation separat bestätigen.",version=manifest.version};
  }finally{if(Directory.Exists(incoming))Directory.Delete(incoming,true);}
 }}
}
