using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading;

// Explicit launcher-only restart, also shared by the crash guard. Never reboots TV.
internal static class LauncherRecovery {
 internal static bool IsVisible(string windows,int pid) {
  if(pid<=0)return false;
  foreach(var line in windows.Split('\n')) {
   var p=Regex.Split(line.Trim(),@"\s+");
   if(p.Length>10&&int.TryParse(p[3],out var windowPid)&&windowPid==pid&&
      line.Contains(" O ")&&line.TrimEnd().EndsWith("Tizen Deck XMB",StringComparison.Ordinal))return true;
  }
  return false;
 }
 static string Run(string file,params string[] args){
  var si=new ProcessStartInfo(file){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};
  foreach(var arg in args)si.ArgumentList.Add(arg);
  using(var p=Process.Start(si)){var output=p.StandardOutput.ReadToEndAsync();var error=p.StandardError.ReadToEndAsync();if(!p.WaitForExit(4000)){p.Kill(true);throw new Exception("Launcher-Aufruf antwortet nicht");}if(p.ExitCode!=0)throw new Exception("Launcher-Aufruf fehlgeschlagen");return output.Result;}
 }
 internal static bool Visible(int pid){try{return ApplicationPolicy.Focus()==pid&&IsVisible(Run("/usr/bin/enlightenment_info","-topvwins"),pid);}catch{return false;}}
 internal static bool AwaitReady(long since,int oldPid=0){
  var deadline=Environment.TickCount64+18000;
  while(Environment.TickCount64<deadline){
   int pid=LauncherGuard.Pid();
   if(pid>0&&pid!=oldPid&&LauncherGuard.Ready(pid,since)&&Visible(pid))return true;
   Thread.Sleep(500);
  }
  return false;
 }
 internal static bool CanFallback(int focus,int deck,int home)=>focus>=0&&(focus==0||focus==deck||focus==home);
 internal static string Fallback(){
  LauncherGuard.Disarm();
  if(!CanFallback(ApplicationPolicy.Focus(),LauncherGuard.Pid(),LauncherGuard.HomePid()))return "Launcher-Menü nicht bestätigt; aktive App bleibt geöffnet";
  Start(LauncherGuard.Home);return "Launcher-Menü nicht bestätigt; Samsung Home als Rückfall geöffnet";
 }
 internal static void Start(string app){Run("/usr/bin/app_launcher","-s",app);}
 internal static object Restart(){
  if(!LauncherGuard.BeginRecovery())throw new Exception("Launcher-Wiederanlauf läuft bereits");
  try{
   int old=LauncherGuard.Pid();LauncherGuard.Disarm();long since=Environment.TickCount64;
   if(old>0){Run("/usr/bin/app_launcher","-k",LauncherGuard.Deck);for(int i=0;i<10&&LauncherGuard.Pid()==old;i++)Thread.Sleep(200);}
   Start(LauncherGuard.Deck);
   if(AwaitReady(since,old)){LauncherGuard.Message("Launcher neu gestartet; Menü sichtbar und bereit");return new{ready=true,pid=LauncherGuard.Pid(),message="Launcher neu gestartet; Menü sichtbar und bereit"};}
   string message=Fallback();LauncherGuard.Message(message);
   return new{ready=false,pid=LauncherGuard.Pid(),message};
  }finally{LauncherGuard.EndRecovery();}
 }
}
