using System;
using System.Threading;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Collections.Generic;

internal static class LauncherGuard {
 internal const string Deck="TizenDeckX.XMB", Home="com.samsung.tv.csfs", Recovered="already-restarted";
 internal sealed class Policy {
  readonly Queue<long> failures=new Queue<long>();
  bool visible;int watchedPid, heartbeatPid;long heartbeat, blockedUntil;
  internal void Observe(long now,bool active,int pid){
   // Keep the previous visible PID until Tick can account for a firmware restart.
   if(!visible||now-heartbeat>35000||!active)watchedPid=pid;
   visible=active&&pid>0;heartbeatPid=pid;heartbeat=now;
  }
  internal bool NeedsCheck(long now,int pid)=>visible&&now-heartbeat<=35000&&now>=blockedUntil&&watchedPid>0&&pid!=watchedPid;
  internal object Status(long now)=>new{armed=visible&&now-heartbeat<=35000,watched_pid=watchedPid,heartbeat_pid=heartbeatPid,heartbeat_age_ms=now-heartbeat,failures=failures.Count,cooldown_ms=Math.Max(0,blockedUntil-now)};
  internal string Tick(long now,int pid,bool anotherAppVisible=false,bool focusKnown=true){
   if(anotherAppVisible){visible=false;return null;}
   if(!NeedsCheck(now,pid))return null;
   // Unknown compositor focus cannot justify opening an app. A newly restarted
   // launcher confirming its own visibility can still be counted without that API.
   if(!focusKnown&&(pid<=0||heartbeatPid!=pid))return null;
   while(failures.Count>0&&now-failures.Peek()>120000)failures.Dequeue();
   failures.Enqueue(now);watchedPid=pid;
   if(failures.Count>2){visible=false;blockedUntil=now+120000;return Home;}
   if(pid>0)return Recovered;
   visible=false;return Deck;
  }
 }
 [DllImport("libaul.so.0")] static extern int aul_app_get_pid(string app);
 static readonly object Gate=new object();static readonly Policy State=new Policy();
 static Timer Timer;static string Last="Noch kein Absturz erkannt";static int Busy;
 internal static void Init(){Timer=new Timer(Check,null,5000,5000);}
 internal static object Status(){lock(Gate)return new{message=Last,policy=State.Status(Environment.TickCount64),launcher_pid=aul_app_get_pid(Deck),home_pid=aul_app_get_pid(Home)};}
 internal static object Pulse(bool visible){lock(Gate){State.Observe(Environment.TickCount64,visible,aul_app_get_pid(Deck));return new{message=Last};}}
 internal static void Disarm(){lock(Gate)State.Observe(Environment.TickCount64,false,0);}
 static void Check(object ignored){if(Interlocked.Exchange(ref Busy,1)!=0)return;try{
  int pid=aul_app_get_pid(Deck);
  lock(Gate)if(!State.NeedsCheck(Environment.TickCount64,pid))return;
  int focus=ApplicationPolicy.Focus();
  bool another=focus>0&&focus!=pid&&focus!=aul_app_get_pid(Home);
  string action;lock(Gate)action=State.Tick(Environment.TickCount64,pid,another,focus>=0);
  if(action==null)return;
  if(action!=Recovered){
   var si=new ProcessStartInfo("/usr/bin/app_launcher"){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};si.ArgumentList.Add("-s");si.ArgumentList.Add(action);
   using(var p=Process.Start(si)){var output=p.StandardOutput.ReadToEndAsync();var error=p.StandardError.ReadToEndAsync();if(!p.WaitForExit(5000)){p.Kill(true);throw new Exception("App-Start antwortet nicht");}if(p.ExitCode!=0)throw new Exception("Wiederanlauf fehlgeschlagen");}
  }
  lock(Gate)Last=action==Recovered?"Launcher-Prozesswechsel nach selbständigem Wiederanlauf erkannt":action==Deck?"Launcher nach Prozessabbruch neu gestartet":"Mehrere Prozessabbrüche: Samsung Home als Rückfall angefordert";
  Console.WriteLine(Last);
 }catch(Exception e){lock(Gate)Last=e.Message;Console.Error.WriteLine("Launcher guard: "+e.Message);}finally{Interlocked.Exchange(ref Busy,0);}}
}
