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
  internal bool Ready(long since,int pid)=>visible&&pid>0&&heartbeatPid==pid&&heartbeat>=since;
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
   if(pid>0&&heartbeatPid==pid)return Recovered;
   visible=false;return Deck;
  }
 }
 [DllImport("libaul.so.0")] static extern int aul_app_get_pid(string app);
 static readonly object Gate=new object();static readonly Policy State=new Policy();
 static Timer Timer;static string Last="Noch kein Absturz erkannt";static int Busy;
 internal static int HomePid()=>aul_app_get_pid(Home);
 internal static int Pid()=>aul_app_get_pid(Deck);
 internal static bool Ready(int pid,long since){lock(Gate)return State.Ready(since,pid);}
 internal static bool BeginRecovery()=>Interlocked.CompareExchange(ref Busy,1,0)==0;
 internal static void EndRecovery()=>Interlocked.Exchange(ref Busy,0);
 internal static void Message(string message){lock(Gate)Last=message;}
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
  if(action==Recovered){
   if(!LauncherRecovery.Visible(pid)) {lock(Gate)Last="Neuer Launcher-Prozess meldet sich; sichtbares Menü noch nicht bestätigt";return;}
   lock(Gate)Last="Launcher-Prozesswechsel bestätigt; Menü sichtbar";
  }else if(action==Deck){
   long since=Environment.TickCount64;LauncherRecovery.Start(Deck);
   if(LauncherRecovery.AwaitReady(since)){lock(Gate)Last="Launcher nach Prozessabbruch sichtbar wiederhergestellt";}
   else {string message=LauncherRecovery.Fallback();lock(Gate)Last=message;}
  }else {LauncherRecovery.Start(Home);lock(Gate)Last="Mehrere Prozessabbrüche: Samsung Home als Rückfall angefordert";}
  Console.WriteLine(Last);
 }catch(Exception e){lock(Gate)Last=e.Message;Console.Error.WriteLine("Launcher guard: "+e.Message);}finally{Interlocked.Exchange(ref Busy,0);}}
}
