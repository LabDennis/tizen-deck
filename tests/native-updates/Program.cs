using System;using System.IO;using System.Text.Json;
class Test {
 static void Main(string[] args) {
  var root=AppContext.BaseDirectory;var pending=Path.Combine(root,"updates","pending");Directory.CreateDirectory(pending);Directory.CreateDirectory(Path.Combine(root,"state"));
  foreach(var file in new[]{"launcher.wgt","manifest.json","manifest.sig"})File.Copy(Path.Combine(args[0],file),Path.Combine(pending,file),true);
  File.Copy(args[1],Path.Combine(root,"state","update-public.der"),true);
  var good=JsonSerializer.Serialize(LauncherUpdates.Status());if(!good.Contains("\"available\":true"))throw new Exception("Valid signature not accepted");
  foreach(var name in new[]{"manifest.json","manifest.sig","launcher.wgt"}){
   var path=Path.Combine(pending,name);var original=File.ReadAllBytes(path);var altered=(byte[])original.Clone();altered[altered.Length-1]^=1;File.WriteAllBytes(path,altered);
   bool refused=false;try{LauncherUpdates.Status();}catch{refused=true;}finally{File.WriteAllBytes(path,original);}
   if(!refused)throw new Exception("Tampering accepted: "+name);
  }
  Directory.Delete(pending,true);var empty=JsonSerializer.Serialize(LauncherUpdates.Status());if(!empty.Contains("\"available\":false"))throw new Exception("Empty stage misreported");
  Console.WriteLine("Valid signed WGT accepted; tampered manifest, signature and payload rejected; empty stage handled.");
 }
}
