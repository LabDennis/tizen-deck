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
- Launcher 1.4.0: Flappy Smasnug unter Extras startet über die native
  Web-Fernbedienung. Ein Sturz zeigt den LCD-Schadenseffekt; Zurück führt wieder
  zur ausgewählten Launcher-Kachel. Durch Compositor-Aufnahmen geprüft.
- Der Eigentümer hat den normalen Menü-Klickton bestätigt. Eigene USB-Sounds
  sind implementiert und mit simulierter Filesystem-API getestet; der echte
  USB-Stick-Test steht noch aus.

- Home-Zuordnung zu Deck: Home aus anderer App und nach Beenden des Launchers,
  Samsung-Fallback und Wiederherstellung der Originalzuordnung am 25.10.2026
  geprüft. Vollständiger Kernel-Neustart mit geänderter Boot-ID und Deck im
  Vordergrund ohne Startbefehl vom PC ebenfalls bestätigt. Auch nach Standby und
  Wake-on-LAN erscheint Deck, wenn die letzte App nicht automatisch gestartet wird. Details: [DEFAULT-HOME.md](DEFAULT-HOME.md).

## Nicht als fertig ausgeben

- Native-Webserver-Autostart weiterhin nicht eingerichtet. Der Launcher startet
  nach geprüftem Kernel-Neustart selbst; physisches Trennen vom Strom ist ungetestet.
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

## Ergänzung vom 25.10.2026: PC Remote und Standby

Die Python-CLI wurde mit verdeckter Passwortabfrage, interaktiven Pfeiltasten,
Beenden und PNG-Aufnahme gegen den nativen HTTPS-Dienst geprüft. Die externe
HTTP-Bridge bestätigt ein bereits eingeschaltetes Gerät, ohne Power-Toggle oder
Weckpakete zu senden. Ein gesonderter echter Standby-Versuch über den Wake-Modulpfad
weckte den Monitor nach 80 Sekunden Schlaf; API, nativer Dienst und SDB waren danach
wieder erreichbar. Details und Grenzen: [PC-REMOTE.md](PC-REMOTE.md).

## Systemausbau 1.7.2

- Eigene Lautstärke-/Stummsteuerung über TVAudioControl und WLAN-/IP-Anzeige.
- Signierte Launcher-Updates prüfen die installierte Version. Hardware-Rückfall
  1.7.0 → 1.6.0 → 1.7.0 bestanden; aktueller Stand 1.7.2 mit Rückfall 1.7.1.
- Drei absichtliche Launcher-Prozessabbrüche wurden gezählt; Samsung Home erschien
  beim dritten. Danach wurde Deck wieder geöffnet. Kein Langzeittest.
- Fünf genau benannte Analyse-/Diagnosedienste lassen sich maskieren und wieder
  freigeben; beide Richtungen gegen den tatsächlichen Dienststatus geprüft.
- 80 schnelle Navigationseingaben: gleicher Launcher-PID; PSS vor/nach/nach acht
  Sekunden Erholung etwa 74,4 / 80,2 / 73,4 MiB. Das ist kein FPS-Benchmark.
- Die begrenzte Gaming-Hub-DLL-Probe lief als UID 5001, nicht als Root. Original
  wiederhergestellt und SHA-256 geprüft. [Boot-Untersuchung](BOOT-RESEARCH.md).
