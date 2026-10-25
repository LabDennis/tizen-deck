using System;using System.Text.Json;using System.Diagnostics;using System.Collections.Generic;
// Uses the compositor's own tested key API; does not register a fake input device.
static class NativeInput {
 static readonly object gate=new object();
 static readonly Dictionary<string,string> keys=new Dictionary<string,string>{{"KEY_UP","Up"},{"KEY_DOWN","Down"},{"KEY_LEFT","Left"},{"KEY_RIGHT","Right"},{"KEY_ENTER","Return"},{"KEY_RETURN","XF86Back"},{"KEY_BACKSPACE","BackSpace"},{"KEY_TAB","Tab"},{"KEY_HOME","XF86Home"},{"KEY_MENU","XF86Menu"},{"KEY_VOLUP","XF86AudioRaiseVolume"},{"KEY_VOLDOWN","XF86AudioLowerVolume"},{"KEY_MUTE","XF86AudioMute"},{"KEY_PLAY","XF86AudioPlay"},{"KEY_PAUSE","XF86AudioPause"},{"KEY_STOP","XF86AudioStop"}};
 static void Key(string name){var start=new ProcessStartInfo("/usr/bin/enlightenment_input_key"){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};start.ArgumentList.Add(name);using(var p=Process.Start(start)){var output=p.StandardOutput.ReadToEndAsync();var error=p.StandardError.ReadToEndAsync();if(!p.WaitForExit(3000)){p.Kill(true);throw new Exception("Compositor antwortet nicht");}if(p.ExitCode!=0)throw new Exception("Compositor hat die Taste nicht angenommen");}}
 public static object Send(JsonElement d){lock(gate){
  string key=d.TryGetProperty("key",out var k)&&k.ValueKind==JsonValueKind.String?k.GetString():null;
  string text=d.TryGetProperty("text",out var t)&&t.ValueKind==JsonValueKind.String?t.GetString():null;
  bool mouse=d.TryGetProperty("mouse",out var m);
  if((key!=null?1:0)+(text!=null?1:0)+(mouse?1:0)>1)throw new Exception("Nur eine Eingabe pro Anfrage");
  if(mouse)throw new Exception("Native Zeigersteuerung noch nicht verifiziert. Pfeiltasten und OK verwenden.");
  if(text!=null)throw new Exception("Native Freitexteingabe noch nicht verifiziert; Bildschirmtastatur verwenden.");
  if(key!=null){if(!keys.ContainsKey(key))throw new Exception("Taste nicht unterstützt");Key(keys[key]);}
  return new{message=key==null?"Compositor-Fernbedienung bereit":"Taste an den Compositor übergeben"};
 }}
}
