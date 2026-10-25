// Adapted from Arn0 Launcher, Copyright (c) 2026 Arnaud Etienne (MIT).
// See NOTICE. Device guard and Home/startup rollback adapted for Tizen Deck.
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;

internal static class Control
{
    private const string Target = "TizenDeckX.XMB";
    private const string TargetPackage = "TizenDeckX";
    private const string MainOperation = "http://tizen.org/appcontrol/operation/main";
    private const string AutoKey = "db/menu/smart_hub/ontvsetting/auto_start";
    private const string LastAppKey = "db/menu/app_booting";
    private const string HomeKey = "db/appfw/home_appid";
    private static readonly string[] Keys = {
        HomeKey,
        "db/appfw/startup_appid",
        "db/appfw/booting_appid_backup",
        "db/first_screen/type"
    };

    [DllImport("libbundle.so.0")] private static extern IntPtr bundle_create();
    [DllImport("libbundle.so.0")] private static extern void bundle_free(IntPtr bundle);
    [DllImport("libaul.so.0")] private static extern int aul_svc_set_operation(IntPtr bundle, string operation);
    [DllImport("libaul.so.0")] private static extern int aul_get_default_app(IntPtr bundle, out IntPtr app);
    [DllImport("libapp-context-manager-common.so")] private static extern byte util_is_appbooting_supported_webapp_byappid(string appId);
    [DllImport("libcapi-appfw-app-operation-tv.so.0")] private static extern int app_operation_set_current_app(string operation, string appId);
    [DllImport("libvconf.so.0")] private static extern IntPtr vconf_get_str(string key);
    [DllImport("libvconf.so.0")] private static extern int vconf_set_str(string key, string value);
    [DllImport("libvconf.so.0")] private static extern int vconf_get_int(string key, out int value);
    [DllImport("libvconf.so.0")] private static extern int vconf_set_int(string key, int value);
    [DllImport("libc")] private static extern uint getuid();
    [DllImport("libc")] private static extern void free(IntPtr ptr);

    private sealed class Snapshot
    {
        public string Home { get; set; }
        public Dictionary<string, string> Strings { get; set; }
        public int Autostart { get; set; }
        public int? LastAppAutostart { get; set; }
    }

    private static void Guard()
    {
        if (IntPtr.Size != 4) throw new Exception("Expected 32-bit process; stopped");
        if (getuid() != 901) throw new Exception("Expected SDK UID 901; stopped");
        string prd = File.ReadAllText("/etc/prd_info.ini");
        if (prd.IndexOf("T-NKLBWWC-1720.7", StringComparison.Ordinal) < 0)
            throw new Exception("Firmware guard mismatch; stopped");
    }

    private static string ReadString(string key)
    {
        IntPtr ptr = vconf_get_str(key);
        if (ptr == IntPtr.Zero) throw new Exception("Cannot read " + key);
        try { return Marshal.PtrToStringAnsi(ptr); }
        finally { free(ptr); }
    }

    private static string ReadHome()
    {
        IntPtr bundle = bundle_create();
        if (bundle == IntPtr.Zero) throw new Exception("bundle_create failed");
        try
        {
            if (aul_svc_set_operation(bundle, MainOperation) != 0)
                throw new Exception("Cannot set Home query operation");
            IntPtr app;
            if (aul_get_default_app(bundle, out app) != 0 || app == IntPtr.Zero)
                throw new Exception("Cannot read Home operation");
            try { return Marshal.PtrToStringAnsi(app); }
            finally { free(app); }
        }
        finally { bundle_free(bundle); }
    }

    private static Snapshot Query()
    {
        var s = new Snapshot { Home = ReadHome(), Strings = new Dictionary<string, string>() };
        foreach (string key in Keys) s.Strings[key] = ReadString(key);
        int autostart;
        if (vconf_get_int(AutoKey, out autostart) != 0) throw new Exception("Cannot read " + AutoKey);
        s.Autostart = autostart;
        int lastApp;
        if (vconf_get_int(LastAppKey, out lastApp) != 0) throw new Exception("Cannot read " + LastAppKey);
        s.LastAppAutostart = lastApp;
        return s;
    }

    private static Snapshot LoadSnapshot(string path)
    {
        var s = JsonSerializer.Deserialize<Snapshot>(File.ReadAllText(path));
        if (s == null || String.IsNullOrWhiteSpace(s.Home) || s.Strings == null ||
            s.Strings.Count != Keys.Length || (s.Autostart != 0 && s.Autostart != 1) ||
            (s.LastAppAutostart.HasValue && s.LastAppAutostart != 0 && s.LastAppAutostart != 1))
            throw new Exception("Invalid original snapshot");
        foreach (string key in Keys)
            if (!s.Strings.ContainsKey(key) || String.IsNullOrWhiteSpace(s.Strings[key]))
                throw new Exception("Snapshot missing " + key);
        return s;
    }

    private static void SetString(string key, string value)
    {
        if (vconf_set_str(key, value) != 0 || ReadString(key) != value)
            throw new Exception("Write/readback failed: " + key);
    }

    private static void SetHome(string appId)
    {
        if (app_operation_set_current_app(MainOperation, appId) != 0 || ReadHome() != appId)
            throw new Exception("Home operation write/readback failed");
    }

    private static void SetInt(string key, int value)
    {
        int actual;
        if ((value != 0 && value != 1) || vconf_set_int(key, value) != 0 ||
            vconf_get_int(key, out actual) != 0 || actual != value)
            throw new Exception("Write/readback failed: " + key);
    }

    private static void SetStartup(int autostart, int? lastApp)
    {
        Exception error = null;
        try { SetInt(AutoKey, autostart); } catch (Exception ex) { error = ex; }
        if (lastApp.HasValue)
        {
            try { SetInt(LastAppKey, lastApp.Value); }
            catch (Exception ex) { error = error == null ? ex : new AggregateException(error, ex); }
        }
        if (error != null) throw error;
    }

    private static void Restore(Snapshot s)
    {
        Exception error = null;
        try { SetHome(s.Home); } catch (Exception ex) { error = ex; }
        try { SetString(HomeKey, s.Strings[HomeKey]); } catch (Exception ex) { error = error == null ? ex : new AggregateException(error, ex); }
        try { SetStartup(s.Autostart, s.LastAppAutostart); } catch (Exception ex) { error = error == null ? ex : new AggregateException(error, ex); }
        if (error != null) throw error;
    }

    private static void RequireOriginal(Snapshot before, Snapshot original)
    {
        if (before.Home != original.Home) throw new Exception("Home operation differs from saved original");
        foreach (string key in new [] { HomeKey })
            if (before.Strings[key] != original.Strings[key])
                throw new Exception("Setting differs from saved original: " + key);
        if (before.Autostart != original.Autostart)
            throw new Exception("Autostart differs from saved original");
        if (original.LastAppAutostart.HasValue && before.LastAppAutostart != original.LastAppAutostart)
            throw new Exception("Last-app autostart differs from saved original");
    }

    private static void RequireRestorable(Snapshot before, Snapshot original)
    {
        if (before.Home != original.Home && before.Home != Target)
            throw new Exception("Home operation changed to an unexpected app; refusing restore");
        foreach (string key in new [] { HomeKey })
            if (before.Strings[key] != original.Strings[key] && before.Strings[key] != Target)
                throw new Exception("Setting changed unexpectedly; refusing restore: " + key);
    }

    private static int Main(string[] args)
    {
        try
        {
            Guard();
            if (args.Length == 1 && args[0] == "query")
            {
                Console.WriteLine("DECK_QUERY=" + JsonSerializer.Serialize(Query()));
                return 0;
            }
            if (args.Length != 2 || (args[0] != "apply" && args[0] != "restore" && args[0] != "verify" && args[0] != "boot-enable" && args[0] != "boot-restore"))
                throw new Exception("Use query or verify|apply|restore|boot-enable|boot-restore SNAPSHOT");
            Snapshot original = LoadSnapshot(args[1]);
            Snapshot before = Query();
            if ((before.Autostart != 0 && before.Autostart != 1) ||
                (before.LastAppAutostart != 0 && before.LastAppAutostart != 1))
                throw new Exception("Unexpected autostart state; stopped");
            if (args[0] == "boot-enable" || args[0] == "boot-restore")
            {
                RequireRestorable(before, original);
                if (!original.LastAppAutostart.HasValue)
                    throw new Exception("Baseline predates standby setup; save original LastAppAutostart before changing it");
                if (args[0] == "boot-enable")
                {
                    if (before.Home != Target || before.Strings[HomeKey] != Target ||
                        !Directory.Exists("/opt/usr/home/owner/apps_rw/" + TargetPackage) ||
                        util_is_appbooting_supported_webapp_byappid(Target) == 0)
                        throw new Exception("Set and test Deck as Home before enabling startup");
                }
                try
                {
                    SetStartup(args[0] == "boot-enable" ? 1 : original.Autostart,
                               args[0] == "boot-enable" ? 0 : original.LastAppAutostart);
                }
                catch (Exception writeError)
                {
                    try { SetStartup(before.Autostart, before.LastAppAutostart); }
                    catch (Exception restoreError) { throw new AggregateException(writeError, restoreError); }
                    throw;
                }
                Console.WriteLine("DECK_BOOT=" + JsonSerializer.Serialize(Query()));
                return 0;
            }
            if (args[0] == "verify")
            {
                RequireOriginal(before, original);
                Console.WriteLine("DECK_BASELINE_MATCH=" + JsonSerializer.Serialize(before));
                return 0;
            }
            if (args[0] == "restore")
            {
                RequireRestorable(before, original);
                Restore(original);
                Console.WriteLine("DECK_RESTORED=" + JsonSerializer.Serialize(Query()));
                return 0;
            }

            RequireOriginal(before, original);
            string appDir = "/opt/usr/home/owner/apps_rw/" + TargetPackage;
            if (!Directory.Exists(appDir)) throw new Exception("Expected launcher package directory missing");
            if (util_is_appbooting_supported_webapp_byappid(Target) == 0)
                throw new Exception("TV says launcher is not eligible for boot; stopped");

            try
            {
                SetHome(Target);
                SetString(HomeKey, Target);
                Snapshot after = Query();
                if (after.Home != Target || after.Strings[HomeKey] != Target)
                    throw new Exception("Applied values failed verification");
                Console.WriteLine("DECK_APPLIED=" + JsonSerializer.Serialize(after));
                return 0;
            }
            catch
            {
                try
                {
                    Restore(original);
                    Console.Error.WriteLine("Original values restored and verified after apply failure");
                }
                catch (Exception restoreError)
                {
                    Console.Error.WriteLine("URGENT: automatic restore failed: " + restoreError);
                }
                throw;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("DECK_ABORTED=" + ex);
            return 1;
        }
    }
}
