# Tizen Deck als Home-App

Auf dem **Samsung M7 LS32BM700UUXEN, T-NKLBWWC-1720.7, Tizen 6.5** wurde am
25.10.2026 die Home-Zuordnung zu `TizenDeckX.XMB` geändert und am Gerät geprüft.
Die Home-Taste öffnet Deck aus dem Browser und aus Samsung Home. Sie startet Deck
auch nach vollständigem Beenden der Launcher-App neu. Das ist kein Neustart des
Monitors: Kaltstart-Autostart und Verhalten nach einem Firmwareupdate sind damit
nicht bestätigt.

**Samsung Home bleibt installiert.** Im Launcher führt
**Einstellungen → Samsung Home** zu `com.samsung.tv.csfs`. Auch dieser Weg und die
anschließende Rückkehr mit Home wurden getestet. Vor einer Deinstallation von
Deck zuerst die ursprüngliche Home-Zuordnung wiederherstellen.

## Geänderte Werte

| Zuordnung | Vorher | Nachher |
| --- | --- | --- |
| Standard-App für `http://tizen.org/appcontrol/operation/main` | `com.samsung.tv.csfs` | `TizenDeckX.XMB` |
| `db/appfw/home_appid` | `com.samsung.tv.csfs` | `TizenDeckX.XMB` |

`auto_start` blieb auf seinem vorgefundenen Wert `0`. `startup_appid`,
`booting_appid_backup`, `first_screen/type` und Custom-App-/Hotel-Einstellungen
wurden nicht geschrieben. Keine Systempartition wurde verändert.

## Rückweg auf dem bereits vorbereiteten Monitor

Aus dem konfigurierten Companion-Checkout:

```sh
.venv/bin/python home_control.py query
.venv/bin/python home_control.py restore
```

Der zweite Aufruf stellt die beiden ursprünglichen Zuordnungen aus der privaten
Gerätesicherung wieder her. Er benötigt die bestehende SDK-Verbindung, aber
weder einen laufenden Deck-Launcher noch den nativen Deck-Webserver. Der Helfer
und `baseline.json` liegen auf diesem Monitor unter
`/opt/usr/home/owner/share/deck-home-control/`; eine zweite Kopie liegt lokal.
Eine frische Installation dieses Repositories erzeugt diese Sicherung **nicht**.
Keine Baseline eines fremden Geräts verwenden.

## Verfahren für eine neue Einrichtung

`home-control/Control.cs` enthält die eng begrenzten Operationen `query`,
`verify BASELINE`, `apply BASELINE` und `restore BASELINE`. Der Helfer verwendet
die vorhandenen Tizen-Bibliotheken und läuft als SDK-Benutzer UID 901. Er prüft
32-Bit-Laufzeit und die oben genannte Firmwarefamilie; das ersetzt keine Prüfung
der tatsächlichen Modellnummer. Es gibt keine Kompatibilitätszusage für andere
Monitore. Herkunft und Lizenz stehen in `home-control/NOTICE`.

1. Kompilieren: `dotnet publish home-control/Control.csproj -c Release -o home-control/dist`.
2. Modell, Firmware, installierte App und funktionierenden Samsung-Rückweg prüfen.
   Über den vorhandenen SDK-Runner `query` ausführen, dessen JSON-Ausgabe als
   private `baseline.json` auf PC und Monitor sichern. `verify` vergleicht die
   beiden Home-Zuordnungen und den unveränderten Auto-Start-Wert mit der Sicherung. Weitere gelesene Werte dienen der
   Dokumentation und werden nicht zurückgeschrieben.
3. Einen vom Launcher unabhängigen Rückstelltimer vor `apply` einrichten und
   seine erfolgreiche Ausführung prüfen. Auf dem Testgerät funktionierte ein
   systemd-Timer mit `User=sdk`, `SmackProcessLabel=System` und dem Aufruf
   `dotnet …/DeckHomeControl.dll restore …/baseline.json`.
4. `apply` prüft App-Verzeichnis und Boot-Eignung, schreibt nur die zwei
   Home-Zuordnungen und liest beide zurück. Bei Fehlern versucht es die
   Rückstellung beider Werte. Ein Fehler bei einer Rückstellung verhindert nicht
   den Versuch, den anderen Wert zu reparieren.
5. Home aus einer anderen App, Neustart einer beendeten Launcher-App und
   Samsung-Fallback prüfen. Erst nach erfolgreicher Prüfung Timer stoppen und
   Endzustand erneut lesen. Auch die tatsächliche Rückstellung von Deck zu
   Samsung wurde am Testgerät erfolgreich ausgeführt.

Der bei der Einrichtung verwendete Timer war ein **Laufzeit-Timer**. Er überlebt
keinen vollständigen Neustart des Monitors und ist nach Abschluss deaktiviert.
Es wurde kein dauerhafter Absturz-Wächter eingerichtet. Die SDK-Rückstellhilfe
bleibt erhalten; sie setzt Erreichbarkeit und aktiven Entwicklermodus voraus.
