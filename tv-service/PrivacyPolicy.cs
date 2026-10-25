using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;
using System.Text.Json;

// Runtime service masks, never deletion of firmware binaries or broad process matching.
internal static class PrivacyPolicy {
 internal static readonly string[] Units={"contents-recognition-service.service","abnormal_user_behavior.service","emergency-log-transmitter.service","media-analytics-service.service","kpi-logging.service"};
 internal sealed class State {
  public bool enabled {get;set;}
  public List<string> owned {get;set;}=new List<string>();
  public List<string> active {get;set;}=new List<string>();
 }
 static readonly object Gate=new object();
 static string PathName=>Path.Combine(AppContext.BaseDirectory,"state","privacy.json");
 static State Current=new State();
 static string LastError="";
 static string Run(params string[] args) {
  var si=new ProcessStartInfo("/bin/systemctl"){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};
  foreach(var a in args)si.ArgumentList.Add(a);
  using(var p=Process.Start(si)) {
   var output=p.StandardOutput.ReadToEndAsync();var error=p.StandardError.ReadToEndAsync();
   if(!p.WaitForExit(4000)){p.Kill(true);throw new Exception("Dienstverwaltung antwortet nicht");}
   if(p.ExitCode!=0)throw new Exception("Dienstverwaltung: "+String.Join(" ",args)+" fehlgeschlagen");
   return output.Result.Trim();
  }
 }
 static string Value(string unit,string property){return Run("show",unit,"-p",property,"--value");}
 static void Save(){var temp=PathName+".new";File.WriteAllText(temp,JsonSerializer.Serialize(Current));File.Move(temp,PathName,true);}
 public static void Init(){lock(Gate){try{
  if(File.Exists(PathName))Current=JsonSerializer.Deserialize<State>(File.ReadAllText(PathName));
  if(Current==null||Current.owned==null||Current.active==null||Current.owned.Concat(Current.active).Any(u=>!Units.Contains(u)))throw new Exception("Ungültige Datenschutz-Sicherung");
  if(Current.enabled)Apply();
 }catch(Exception e){LastError=e.Message;Console.Error.WriteLine("Privacy policy: "+LastError);}}}
 static void Apply(){
  foreach(var u in Units){
   var load=Value(u,"LoadState");if(load=="not-found")continue;
   if(!Current.owned.Contains(u)) {
    // A mask from another administrator does not become ours to remove.
    if(load=="masked")continue;
    if(Value(u,"ActiveState")=="active"&&!Current.active.Contains(u))Current.active.Add(u);
    Current.owned.Add(u);Save();
   }
   Run("mask","--runtime",u);Run("stop",u);
   if(Value(u,"LoadState")!="masked"||Value(u,"ActiveState")=="active")throw new Exception("Sperre nicht bestätigt: "+u);
  }
 }
 public static object Set(bool enabled){lock(Gate){
  Current.enabled=enabled;Save();LastError="";
  try{
   if(enabled)Apply();
   else foreach(var u in Current.owned.ToArray()){
    Run("unmask","--runtime",u);
    if(Current.active.Contains(u))Run("start",u);
    Current.owned.Remove(u);Current.active.Remove(u);Save();
   }
  }catch(Exception e){LastError=e.Message;throw;}
  return Status();
 }}
 public static object Status(){lock(Gate){
  var services=new List<object>();foreach(var u in Units){try{services.Add(new{name=u,active=Value(u,"ActiveState"),masked=Value(u,"LoadState")=="masked"});}catch{services.Add(new{name=u,active="unknown",masked=false});}}
  return new{enabled=Current.enabled,services,error=LastError,message="Laufzeitsperren für fünf geprüfte Analyse-/Diagnosedienste. Werden beim Start des TV-Dienstes erneut angewendet. Kein Nachweis vollständiger Trackingfreiheit."};
 }}
}
