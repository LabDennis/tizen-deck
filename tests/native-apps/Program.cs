using System;using System.Collections.Generic;
class Tests {
 static void Assert(bool value,string message){if(!value)throw new Exception(message);}
 static void Main(){
  var running=new Dictionary<string,int>{{ApplicationPolicy.Deck,10},{"org.tizen.browser",20},{"kIciSQlYEM.plex",30},{"com.samsung.tv.csfs",40},{"org.tizen.tv-viewer",50}};
  var stopped=new List<string>();int focus=10;
  Func<string,int> pid=id=>running.TryGetValue(id,out var n)?n:-1;
  Action<string> stop=id=>{stopped.Add(id);running.Remove(id);};
  var r=ApplicationPolicy.Clean("kIciSQlYEM.plex",pid,()=>focus,stop);
  Assert(r.closed.Count==1&&r.closed[0]=="org.tizen.browser","close only other known app");
  Assert(running.ContainsKey("com.samsung.tv.csfs")&&running.ContainsKey(ApplicationPolicy.Deck)&&running.ContainsKey("org.tizen.tv-viewer"),"system/fallback/Deck preserved");
  try{ApplicationPolicy.Clean("x;reboot",pid,()=>focus,stop);throw new Exception("bad keep accepted");}catch(ArgumentException){}
  focus=30;try{ApplicationPolicy.Clean("",pid,()=>focus,stop);throw new Exception("background cleanup accepted");}catch(InvalidOperationException){}
  focus=10;running["org.tizen.browser"]=20;stopped.Clear();int checks=0;
  r=ApplicationPolicy.Clean("",pid,()=>++checks<=2?10:30,stop);
  Assert(r.interrupted&&stopped.Count==1&&running.ContainsKey("org.tizen.browser"),"stop when focus changes mid-cleanup");
  r=ApplicationPolicy.Clean("",pid,()=>10,id=>{});Assert(r.failed.Contains("org.tizen.browser"),"do not claim a still-running app closed");
  running["org.tizen.browser"]=70;var reserves=new Dictionary<string,int>();
  r=ApplicationPolicy.Clean("",pid,()=>10,id=>running[id]=80,reserves);
  Assert(r.closed.Contains("org.tizen.browser")&&r.restarted.Contains("org.tizen.browser"),"report replacement process separately");
  r=ApplicationPolicy.Clean("",pid,()=>10,id=>{throw new Exception("reserve must not be killed");},reserves);
  Assert(r.retained.Contains("org.tizen.browser")&&r.failed.Count==0,"do not churn firmware reserve processes");
  ApplicationPolicy.Clean("org.tizen.browser",pid,()=>10,stop,reserves);
  r=ApplicationPolicy.Clean("",pid,()=>10,stop,reserves);Assert(r.closed.Contains("org.tizen.browser"),"a new Deck launch allows closing the next user session");
  r=ApplicationPolicy.Clean("",pid,()=>10,stop,reserves);
  Assert(r.closed.Count==0,"an absent delayed reserve is not an app to close");
  running["org.tizen.browser"]=90;
  r=ApplicationPolicy.Clean("",pid,()=>10,id=>{throw new Exception("delayed reserve must not be killed");},reserves);
  Assert(r.retained.Contains("org.tizen.browser")&&r.failed.Count==0,"adopt a replacement that arrives after the initial cleanup");
  ApplicationPolicy.Clean("org.tizen.browser",pid,()=>10,stop,reserves);
  r=ApplicationPolicy.Clean("",pid,()=>10,stop,reserves);
  Assert(r.closed.Contains("org.tizen.browser"),"launching a delayed reserve through Deck restores normal cleanup");
  Console.WriteLine("Application policy: allowlist, target exclusion, system fallback, foreground guard, focus race and verified closure passed");
 }
}
