using System;
class Tests {
 static void Equal(string a,string b){if(a!=b)throw new Exception("Expected "+b+", got "+a);}
 static void Main(){var p=new LauncherGuard.Policy();
  Equal(p.Tick(0,0),null);p.Observe(0,true,41);Equal(p.Tick(5000,41),null);
  p.Observe(6000,false,41);Equal(p.Tick(10000,0),null); // legitimate departure
  p.Observe(20000,true,51);Equal(p.Tick(60000,0),null); // stale context never steals focus
  p.Observe(70000,true,61);Equal(p.Tick(75000,0),"TizenDeckX.XMB");Equal(p.Tick(76000,0),null); // single attempt
  p.Observe(80000,true,71);Equal(p.Tick(85000,0),"TizenDeckX.XMB");
  p.Observe(90000,true,81);Equal(p.Tick(95000,0),"com.samsung.tv.csfs");
  p.Observe(100000,true,91);Equal(p.Tick(105000,0),null); // cooldown
  p.Observe(220000,true,101);Equal(p.Tick(225000,0),"TizenDeckX.XMB"); // rolling window reset
  p.Observe(230000,true,111);Equal(p.Tick(235000,0,true),null);Equal(p.Tick(240000,0),null);
  // Firmware can respawn between ticks, including a heartbeat from the new PID.
  p=new LauncherGuard.Policy();p.Observe(0,true,1);p.Observe(4000,true,2);
  Equal(p.Tick(5000,2),LauncherGuard.Recovered);Equal(p.Tick(6000,2),null);
  p.Observe(8000,true,3);Equal(p.Tick(10000,3,false,false),LauncherGuard.Recovered);
  p.Observe(12000,true,4);Equal(p.Tick(15000,4,false,false),LauncherGuard.Home);
  // Unknown focus and no fresh confirmation must never steal a foreground app.
  p=new LauncherGuard.Policy();p.Observe(0,true,1);
  Equal(p.Tick(5000,0,false,false),null);Equal(p.Tick(6000,2,false,false),null);
  p.Observe(7000,false,2);p.Observe(9000,true,3);Equal(p.Tick(10000,3),null);
  p.Observe(12000,true,4);Equal(p.Tick(15000,4,true),null);
  Console.WriteLine("Guard: process loss and between-tick respawns, hidden/stale/unknown-focus suppression, bounded retries, Samsung fallback and cooldown passed.");
 }
}
