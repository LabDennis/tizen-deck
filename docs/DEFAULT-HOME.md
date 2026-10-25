# Tizen Deck als Home-App und beim Einschalten

Auf dem **Samsung M7 LS32BM700UUXEN, T-NKLBWWC-1720.7, Tizen 6.5** wurden am
25.10.2026 die Home-Zuordnung und der Launcher-Autostart am Gerät geprüft.
Die Home-Taste öffnet Deck aus dem Browser und aus Samsung Home. Sie startet Deck
auch nach vollständigem Beenden der Launcher-App neu.

Nach einem **vollständigen Kernel-Neustart** stand Deck ohne Home-Eingabe und ohne
App-Start vom PC im Vordergrund. Eine geänderte Boot-ID belegt den Neustart;
Prozess/Fokus und eine spätere Bildschirmaufnahme belegen den Launcher-Start.
Die erste erfolgreiche SDK-Abfrage erfolgte bei 47 Sekunden Systemlaufzeit. Das
ist eine Beobachtungszeit, keine gemessene Startdauer. Ein Einschalten nach
physischem Trennen vom Strom und Verhalten nach Firmwareupdates sind ungetestet.

Auch **Standby → Wake-on-LAN** wurde aus einem zuvor geöffneten Browser geprüft.
Mit aktivem „letzte App automatisch starten“ kehrte der Browser zurück. Nach
Abschalten dieser separaten Option stand Deck im Vordergrund; die Boot-ID blieb
beim Standby unverändert. Das betrifft den geprüften Netzwerk-Weckweg, andere
Weckquellen können von der Firmware gesondert behandelt werden.

**Samsung Home bleibt installiert.** Im Launcher führt
**Einstellungen → Samsung Home** zu `com.samsung.tv.csfs`. Auch dieser Weg und die
anschließende Rückkehr mit Home wurden getestet. Vor einer Deinstallation von
Deck zuerst die ursprünglichen Einstellungen wiederherstellen.

## Geänderte Werte

| Zuordnung | Vorher | Nachher |
| --- | --- | --- |
| Standard-App für `http://tizen.org/appcontrol/operation/main` | `com.samsung.tv.csfs` | `TizenDeckX.XMB` |
| `db/appfw/home_appid` | `com.samsung.tv.csfs` | `TizenDeckX.XMB` |
| `db/menu/smart_hub/ontvsetting/auto_start` | `0` | `1` |
| `db/menu/app_booting` (letzte App automatisch starten) | `1` | `0` |

`startup_appid`, `booting_appid_backup`, `first_screen/type` und Custom-App-/Hotel-
Einstellungen wurden nicht direkt geschrieben. Samsung aktualisierte beim Start
selbst `startup_appid` und `booting_appid_backup` auf Deck. Keine Systempartition
wurde verändert und keine Firmware geflasht.

## Rückweg auf dem bereits vorbereiteten Monitor

Aus dem konfigurierten Companion-Checkout:

```sh
.venv/bin/python home_control.py query
.venv/bin/python home_control.py boot-restore  # beide ursprünglichen Startoptionen zurücksetzen
.venv/bin/python home_control.py boot-enable   # Autostart einschalten, wenn Deck bereits Home ist
.venv/bin/python home_control.py restore       # Home-Zuordnungen UND beide Startoptionen zurücksetzen
```

Diese Befehle benötigen die bestehende SDK-Verbindung, aber weder einen laufenden
Deck-Launcher noch den nativen Deck-Webserver. Der Helfer und `baseline.json`
liegen auf diesem Monitor unter `/opt/usr/home/owner/share/deck-home-control/`;
eine zweite Kopie liegt lokal. Die Sicherung enthält die Werte **vor** unserer
Einrichtung. `LastAppAutostart` wurde vor der Standby-Änderung gesondert gesichert.
Ältere Baselines ohne diesen Wert können weiterhin Home zurückstellen, verweigern
aber die neuen `boot-*`-Befehle, bis der echte ursprüngliche Wert ergänzt wurde. Eine frische Installation dieses Repositories erzeugt diese Sicherung
**nicht**. Keine Baseline eines fremden Geräts verwenden.

## Verfahren für eine neue Einrichtung

`home-control/Control.cs` bietet `query`, `verify BASELINE`, `apply BASELINE`,
`restore BASELINE`, `boot-enable BASELINE` und `boot-restore BASELINE`. Der Helfer
verwendet vorhandene Tizen-Bibliotheken und läuft als SDK-Benutzer UID 901. Er prüft
32-Bit-Laufzeit und die oben genannte Firmwarefamilie; das ersetzt keine Prüfung
der tatsächlichen Modellnummer. Es gibt keine Kompatibilitätszusage für andere
Monitore. Herkunft und Lizenz stehen in `home-control/NOTICE`.

1. Kompilieren: `dotnet publish home-control/Control.csproj -c Release -o home-control/dist`.
2. Modell, Firmware, installierte App und funktionierenden Samsung-Rückweg prüfen.
   Über den vorhandenen SDK-Runner `query` ausführen, dessen JSON-Ausgabe als
   private `baseline.json` auf PC und Monitor sichern. `verify` vergleicht die
   beiden Home-Zuordnungen und die gesicherten Startoptionen mit der Sicherung. Weitere
   gelesene Werte dienen der Dokumentation und werden nicht zurückgeschrieben.
3. Einen vom Launcher unabhängigen Rückstelltimer vor `apply` einrichten und
   seine erfolgreiche Ausführung prüfen. Auf dem Testgerät funktionierte ein
   systemd-Timer mit `User=sdk`, `SmackProcessLabel=System` und dem Aufruf
   `dotnet …/DeckHomeControl.dll restore …/baseline.json`.
4. `apply` prüft App-Verzeichnis und Boot-Eignung, schreibt nur die zwei
   Home-Zuordnungen und liest beide zurück. Bei Fehlern versucht es die
   Rückstellung. Ein einzelner Rückstellfehler verhindert nicht die Versuche,
   die anderen verwalteten Werte zu reparieren.
5. Home aus einer anderen App, Neustart einer beendeten Launcher-App und
   Samsung-Fallback prüfen. Erst nach erfolgreicher Prüfung Timer stoppen und
   Endzustand erneut lesen. Auch die tatsächliche Rückstellung von Deck zu
   Samsung wurde am Testgerät erfolgreich ausgeführt.
6. Für Autostart separat `boot-enable` ausführen. Es setzt Deck als Home sowie
   ein installiertes, bootfähiges Paket voraus. Es schaltet den Home-Autostart ein
   und den Autostart der zuletzt genutzten App aus. Bei einem Schreibfehler
   versucht es, beide zuvor gelesenen Startoptionen wiederherzustellen. Vor dem Neustart
   einen **externen** Rückweg über SDK vorbereiten: ein Timer in `/run` überlebt
   den Neustart nicht. Nach dem Start Boot-ID, Fokus, Prozess und Bild prüfen,
   ohne zuvor Deck vom PC aus zu starten.

Der bei der Home-Einrichtung verwendete Timer war ein **Laufzeit-Timer**. Er ist
nach Abschluss deaktiviert. Es wurde kein dauerhafter Absturz-Wächter eingerichtet.
Die SDK-Rückstellhilfe bleibt erhalten; sie setzt Erreichbarkeit und aktiven
Entwicklermodus voraus. Auch nach dem geprüften vollständigen Neustart blieb SDK
auf diesem Monitor erreichbar.

## Grenze zum nativen Webserver

Der erfolgreiche Autostart gilt für die **Launcher-App**. Der native Deck-Webserver
verwendet weiterhin eine Systemd-Unit unter `/run`: Nach dem vollständigen Neustart
war sie verschwunden, und der Dienst wurde separat vom PC wieder gestartet.
Ein dauerhafter Boot-Hook für diesen Root-Dienst ist weiterhin nicht eingerichtet.
Die WGT-Installation alleine ändert weder Home-Zuordnung noch Autostart.
