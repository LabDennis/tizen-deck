using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Reflection;

internal static class ServiceHealth {
 internal static string Version => typeof(ServiceHealth).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "unknown";
 internal static object Status()=>new{ready=true,version=Version,pid=System.Diagnostics.Process.GetCurrentProcess().Id,boot_id=File.ReadAllText("/proc/sys/kernel/random/boot_id").Trim()};
 // Installer probes the authenticated controller, not merely systemd's process state.
 internal static int Probe(){try{
  var token=File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"state","control-token")).Trim();
  using(var c=new TcpClient()) {
   c.SendTimeout=2000;c.ReceiveTimeout=2000;
   var task=c.ConnectAsync("127.0.0.1",8844);if(!task.Wait(2000))return 1;task.GetAwaiter().GetResult();
   using(var s=c.GetStream()){
    var req=Encoding.ASCII.GetBytes("GET /health HTTP/1.1\r\nHost: localhost\r\nX-Deck-Control: "+token+"\r\nConnection: close\r\n\r\n");s.Write(req,0,req.Length);
    using(var reader=new StreamReader(s)){
     var data=reader.ReadToEnd();var split=data.IndexOf("\r\n\r\n",StringComparison.Ordinal);
     if(!data.StartsWith("HTTP/1.1 200 ")||split<0)return 1;
     var body=JsonDocument.Parse(data.Substring(split+4)).RootElement;
     return body.GetProperty("ready").GetBoolean()&&body.GetProperty("version").GetString()==Version?0:1;
    }
   }
  }
 }catch{return 1;}}
}
