using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;

// Only user-facing apps explicitly offered by Deck. Never enumerate/kill system services.
internal static class ApplicationPolicy
{
    internal const string Deck = "TizenDeckX.XMB";
    internal static readonly string[] Managed = {
        "kIciSQlYEM.plex", "AprZAARz4r.Jellyfin", "9Ur5IzDKqV.TizenYouTube",
        "MoonLightS.MoonlightWasm", "org.tizen.browser",
        "org.tizen.ScreenMirroring-app-tv", "com.samsung.tv.coba.remoteworkspace"
    };
    static readonly HashSet<string> AllowedKeep = new HashSet<string>(Managed) {
        "", Deck, "org.tizen.tv-viewer", "com.samsung.tv.csfs", "org.tizen.menu", "org.tizen.btaudiolist"
    };
    internal sealed class Result {
        public List<string> closed { get; set; } = new List<string>();
        public List<string> restarted { get; set; } = new List<string>();
        public List<string> retained { get; set; } = new List<string>();
        public List<string> failed { get; set; } = new List<string>();
        public bool interrupted { get; set; }
    }
    [DllImport("libaul.so.0")] static extern int aul_app_get_pid(string id);
    static readonly object Gate = new object();
    static readonly Dictionary<string,int> ReservePids = new Dictionary<string,int>();

    internal static Result Clean(string keep, Func<string,int> pid, Func<int> focus, Action<string> stop, IDictionary<string,int> reserves = null)
    {
        if (keep == null || !AllowedKeep.Contains(keep)) throw new ArgumentException("Unbekannte Ziel-App");
        var result = new Result();
        reserves = reserves ?? new Dictionary<string,int>();
        int deckPid = pid(Deck);
        if (deckPid <= 0 || focus() != deckPid) throw new InvalidOperationException("Aufräumen nur bei sichtbarem Deck-Menü");
        // Selecting an app through Deck marks its next session as user activity again.
        reserves.Remove(keep);
        foreach (var app in Managed)
        {
            int before = pid(app);
            if (app == keep || before <= 0) continue;
            if (reserves.TryGetValue(app, out var cached) && (cached == before || (cached < 0 && before != -cached))) {
                // Firmware can preload the replacement seconds after the old PID disappears.
                // A negative PID records that completed termination until a replacement arrives.
                reserves[app] = before;
                result.retained.Add(app); continue;
            }
            // Recheck before every termination: a delayed request must not kill a newly active app.
            if (focus() != deckPid || pid(Deck) != deckPid) { result.interrupted = true; break; }
            try {
                stop(app); int after = pid(app);
                if (after <= 0 || after != before) {
                    result.closed.Add(app);
                    if (after > 0) { result.restarted.Add(app); reserves[app] = after; }
                    else reserves[app] = -before;
                } else result.failed.Add(app);
            }
            catch { result.failed.Add(app); }
        }
        return result;
    }
    static string Execute(string program, string[] args) {
        var start = new ProcessStartInfo(program) { UseShellExecute=false, RedirectStandardOutput=true, RedirectStandardError=true };
        foreach(var arg in args) start.ArgumentList.Add(arg);
        using(var p = Process.Start(start)) {
            var output=p.StandardOutput.ReadToEndAsync();var error=p.StandardError.ReadToEndAsync();
            if(!p.WaitForExit(2000)){p.Kill(true);throw new Exception("App-Verwaltung antwortet nicht");}
            if(p.ExitCode!=0)throw new Exception("App-Verwaltung hat den Aufruf abgelehnt");
            return output.Result;
        }
    }
    static int Focus() {
        var text=Execute("/usr/bin/dbus-send",new[]{"--system","--print-reply","--reply-timeout=1000","--dest=org.enlightenment.wm","/org/enlightenment/wm","org.enlightenment.wm.proc.GetFocusProc"});
        var m=Regex.Match(text,@"\bint32 (\d+)");return m.Success?int.Parse(m.Groups[1].Value):-1;
    }
    internal static Result Clean(string keep) {
        lock(Gate) return Clean(keep,aul_app_get_pid,Focus,app=>{
            int before=aul_app_get_pid(app);
            Execute("/usr/bin/app_launcher",new[]{"-t",app});
            for(int i=0;i<20&&aul_app_get_pid(app)==before;i++)Thread.Sleep(100);
        },ReservePids);
    }
}
