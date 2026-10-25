# Getesteter Monitor und Entwicklungsnachweis

Stand: 25. Oktober 2026. Dieses Projekt wurde für ein konkretes, vom Eigentümer
bereitgestelltes Gerät entwickelt. Eine ähnliche Modellnummer ist keine getestete
Kompatibilität.

| Merkmal | Tatsächlich getesteter Stand |
|---|---|
| Gerät | Samsung Smart Monitor M7, 32 Zoll |
| Vollständiger Modellcode | `LS32BM700UUXEN` |
| Firmware | `T-NKLBWWC-1720.7` |
| Plattform | Tizen 6.5 |
| Userspace / Laufzeit | ARMv7, .NET Core 3.1.3 |
| Kernel | Linux 5.4.77 |
| Angezeigte Bildschirmauflösung | 3840 × 2160 |
| Compositor-Aufnahme im Test | PNG, 1920 × 1080 |
| Root-Verfahren | Archive-Launcher-Route über `samsung-tv-root` |
| Root-Ergebnis | UID 0, SMACK-Kontext `User`; temporäre Aufträge mit Cleanup geprüft |

Die zuvor installierte Version 1470.0 wurde nicht als Root-Ziel verifiziert.
Andere Monitore, Größen, Regionen und Firmwarestände sind hier **ungetestet**.
Seriennummer, MAC-Adressen, private Netzadressen und Konten werden nicht benötigt
und gehören nicht in Kompatibilitätsberichte.

## Am Gerät bestätigt

- Launcher und Plex-Start; Jellyfin-Installation und Server-Anmeldebildschirm.
- Temporärer Root-Zugriff, Diagnose und Bildschirmaufnahme.
- Installation des nativen .NET-Webdiensts; HTTPS-Login, geschützter PNG-Zugriff
  und Live-Metriken aus `/proc`.
- Bluetooth-Audio-Liste über `org.tizen.btaudiolist`.
- Der Compositor-Key-Aufruf `enlightenment_input_key Right` bewegt tatsächlich
  die Launcher-Kategorie; die native Web-Fernbedienung bewegt die Kategorie ebenfalls nachweislich.

## Nicht als fertig ausgeben

- Kein bestätigter Kaltstart-Autostart und kein Ersatz der Home-Taste.
- Kein dauerhaft entsperrter Bootloader, kein getestetes Custom-Firmware-Image.
- Native Maus-/Texteingabe und alle Controller-Modelle sind nicht vollständig
  hardwareverifiziert. Virtuelle uinput-Tasten alleine bewegten das Menü nicht.
- Bildschirmaufnahmen sind kein flüssiger Videostream; DRM-/HDMI-Ebenen unklar.
- Kein garantiertes Aufwachen per Wecker, kein fertiger USB-Emulator-Launcher,
  kein fertiges PiP zwischen beliebigen Apps.

## Firmwareanalyse

Eine private eMMC-Sicherung wurde aufgenommen und die Empfangsdaten gegen die
Quellstream-Prüfsummen geprüft. Die Systempartition wurde offline entpackt.
Der VDFS-Parser musste für Hash-/Signaturtabellen korrigiert werden; danach wurden
keine Extraktionsfehler gemeldet. Das beweist weder die Gültigkeit eines neu
gebauten Images noch seine Bootfähigkeit. Eigentümer, Dateirechte, Extended
Attributes, Signaturen und Recovery müssen vor einem Rebuild getrennt geprüft
werden. Es wurde keine Custom-Firmware geflasht.

Für einen neuen Gerätebericht bitte Modell, Firmware, Runtime und bereinigte
Ergebnisse von `id`/Diagnose angeben. Keine Roh-Dumps oder Zugangsdaten hochladen.
