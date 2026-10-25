# Root und Einrichtung: Samsung LS32BM700UUXEN

Diese Anleitung beschreibt den an **LS32BM700UUXEN / T-NKLBWWC-1720.7** geprüften
Weg. Sie setzt ein eigenes bzw. ausdrücklich freigegebenes Gerät voraus. Der
[Gerätenachweis](TESTED-MONITOR.md) trennt bestätigte Ergebnisse von offenen Punkten.

## 1. Entwicklermodus und Netzwerk

1. In Samsung **Apps** die Ziffernfolge **12345** eingeben.
2. Developer Mode einschalten; die vom Monitor erreichbare IPv4-Adresse des
   Linux-PCs eintragen. Bei mehreren VLANs nicht die Router-IP eintragen.
3. Monitor neu starten. Danach Apps erneut öffnen und den Modus überprüfen.
4. PC → Monitor benötigt TCP 26101. Diagnose/Remote nutzen außerdem 8001/8002.
   Der hier verwendete Wrapper benötigt Rückverbindungen Monitor → PC auf
   TCP 1764 (SDK) und TCP 1763 (Root). Freigaben nur zwischen diesen Geräten.

Ein leeres Apps-Menü oder Fehler 22 ist kein Root-Nachweis. Bei DNS-Filtern zuerst
gezielt prüfen, ob der App-Store geblockt ist. Developer Mode aktiviert weder
Root noch automatisch SSH. Die genaue Menüführung kann sich nach Updates ändern.

## 2. Werkzeuge auf Linux x86_64 installieren

Voraussetzungen: Python 3.12+, venv-Unterstützung, Git und curl.

```sh
git clone https://github.com/LabDennis/tizen-deck.git
cd tizen-deck
bash install.sh 192.0.2.10 --root-support
```

`192.0.2.10` ist eine Beispieladresse und muss ersetzt werden. Solange das Repo
privat ist, braucht der GitHub-Account Leserechte. Der Installer lädt die im Code
festgelegten Upstream-Versionen und prüft die Download-Hashes. Er führt dabei
noch keinen Root-Auftrag auf dem Monitor aus.

## 3. SDK-Zugriff und anschließend UID 0 prüfen

Aus dem Projektverzeichnis:

```sh
.venv/bin/python - <<'PY'
from tv import TV
result = TV().sdk('id')
print(result)
PY
```

Erst wenn die Rückverbindung funktioniert, den expliziten Root-Test ausführen:

```sh
.venv/bin/python - <<'PY'
from tv import TV
result = TV().root('id; cat /proc/self/attr/current; uname -r')
print(result)
if result['uid'] != 0 or result['exit_code'] != 0 or result['timed_out']:
    raise SystemExit('Root-Test nicht erfolgreich')
if not result['cleaned'] or not result['system_files_unchanged']:
    raise SystemExit('Cleanup prüfen, bevor weitere Aufträge gestartet werden')
PY
```

Am Testgerät wurde UID 0 mit SMACK `User` gemeldet. Bei einem Fehler nicht blind
Firmware downgraden oder fremde Images flashen; zunächst Firmware, Runtime,
Developer-IP, Callback-Routing und die Fehlermeldung prüfen.

## 4. Was dabei technisch passiert

Die Root-Kette stammt aus [chris-ritsen/samsung-tv-root](https://github.com/chris-ritsen/samsung-tv-root),
nicht aus einem neu von Tizen Deck entdeckten Exploit. Unser Wrapper verwendet
seine Archive-Launcher-Route mit den zum Gerät passenden .NET-3.1-Payloads.
Die Kette nutzt die SDB-Einstiegsmöglichkeit, eine temporäre Namespace-/Archiv-
Launcher-Umgebung und einen Rückkanal zum PC. Tizen Deck führt den angeforderten
Befehl aus und wertet anschließend Cleanup und überwachte Systemdateien aus.
Details und Grenzen sind im Upstream-Quellcode und dessen Dokumentation beschrieben.

Das ist kein permanentes Bootloader-Unlock, keine Abschaltung von Secure Boot und
keine Garantie gegen SMACK-Beschränkungen. Die Methode kann durch andere Firmware
geändert oder geschlossen sein. Ein erfolgreicher `id`-Test beweist keine
Bootfähigkeit eines selbstgebauten Linux-Images.

## 5. Webinterface und Launcher

`bash start.sh` startet die externe Oberfläche auf `http://localhost:8765`.
Das zunächst erzeugte Passwort steht nur lokal in `.state/initial-login.txt`.
Die Konsole bietet SDK- und explizit bestätigte Root-Einzelbefehle.

Launcher: [tizen-deck-xmb](https://github.com/LabDennis/tizen-deck-xmb).
Nach dessen Build aus dem Launcher-Verzeichnis:

```sh
bash install.sh 192.0.2.10 --root-deck /absoluter/pfad/tizen-deck
```

Start über die SDK-Konsole: `app_launcher -s TizenDeckX.XMB`.
Samsung Home bleibt erreichbar. Für den optionalen **direkt auf dem TV laufenden**
Root-Webdienst gibt es die separate [TV-Service-Anleitung](TV-SERVICE.md).
Dieser Dienst läuft bis zum Stoppen/Neustart weiter; die Cleanup-Aussagen für
kurze externe Root-Aufträge gelten nicht für diesen separat installierten Dienst.

## 6. Rückweg und private Daten

Launcher bewusst deinstallieren: `pkgcmd -u -n TizenDeckX`. Externe Bridge mit
Strg+C stoppen. Nativen Dienst über das TV-Menü deaktivieren bzw. über eine
berechtigte Root-Sitzung `systemctl stop deck-tv.service` ausführen. Der aktuelle
Dienst besitzt keinen eingerichteten Kaltstart-Hook. Eine Deinstallation des
Launchers entfernt nicht automatisch den separat installierten TV-Dienst.

`.state/`, `.signing/`, Geräte-Backups und Dumps privat sichern und niemals
veröffentlichen. Backups/Partitionsprüfung siehe [GUIDE.md](GUIDE.md#8-backups-und-firmware).
Ein USB-Update-Menü nimmt nicht automatisch unsere eigenen Firmware-Images an.
