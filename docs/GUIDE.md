# Einrichtung und Betrieb

## 1. Modell und Zustand dokumentieren

Unter Einstellungen → Unterstützung → Info Modell und Firmware ablesen. Getestet wurde LS32BM700UUXEN mit T-NKLBWWC-1720.7. Root ist firmwareabhängig. Eine USB-Update-Funktion ist kein allgemeiner Custom-Firmware-Flasher. Diese Anleitung installiert weder Linux noch eine neue Firmware.

## 2. Entwicklermodus

Samsung Apps öffnen und dort die Ziffernfolge 12345 eingeben. Im Entwicklerdialog Developer Mode einschalten, die vom TV erreichbare IPv4-Adresse des PCs eintragen und den Monitor neu starten. Menüpfade und Zifferneingabe unterscheiden sich je nach Fernbedienung und Firmware. Developer Mode gibt nicht automatisch Root oder eine normale SSH-Shell frei.

## 3. Netzwerk

PC → TV: TCP 26101 (SDB), 8001 (Geräteinformationen), 8002 (Fernbedienung). Für Diagnose und temporären Root-Kanal nutzt diese Implementierung TV → PC: TCP 1764 und 1763. Firewall-Freigaben auf genau diese beiden Geräte beschränken. Keine Internet-Portweiterleitung. Bei mehreren VLANs muss der Rückweg funktionieren; die Firewall-Adresse ist nicht die Entwickler-PC-Adresse. Keine Netzwerkregeln werden durch den Installer verändert.

## 4. PC installieren

Python 3.12 oder neuer, Git und curl bereitstellen. `bash install.sh TV_IP` lädt SDB und die festgelegte SDK-Brücke in `.deps/`. `--root-support` lädt zusätzlich das festgelegte Root-Payload-Paket. Private Konfiguration liegt unter `.state/`. Der Installer setzt keinen Systemdienst auf. `bash start.sh` startet die Anwendung nur auf 127.0.0.1:8765. Für einen zweiten Start muss der alte Prozess beendet sein.

## 5. Authentifizierung

Das erste Passwort wird zufällig erzeugt und lokal mit Dateirechten 0600 ausgegeben. Im Webinterface ändern; alternativ bei gestopptem Dienst `bash start.sh --set-password`. Serverseitig wird scrypt verwendet. Sitzungen laufen nach acht Stunden ab. Die UI prüft Origin, Host und CSRF. Nicht über einen offenen Reverse Proxy veröffentlichen. Diese externe Bridge bindet nur an Loopback. Der optionale native TV-Dienst hat einen getrennten HTTPS-LAN-Zugang; siehe TV-SERVICE.md.

## 6. Diagnose und Root

Zuerst eine Übersichtsmessung und im SDK-Modus `id` ausführen. SDK-Diagnose und Root sind unterschiedliche Rechte. Im Konsolenbereich Root explizit auswählen und bestätigen; mit `id` prüfen. Der getestete Zugriff hatte UID 0, aber weiterhin einen SMACK-Kontext. Root ist daher kein Beleg, dass jede Hardware-/Sicherheitsgrenze aufgehoben ist.

Technisch verwendet Tizen Deck das Archive-Root-Verfahren aus samsung-tv-root. Dieses prüft Voraussetzungen und startet den Agenten in einer temporären Umgebung. Nach jedem Auftrag wird aufgeräumt; Prüfsummen der überwachten Systemdateien werden verglichen. Die Konsole startet einzelne Befehle, kein dauerhaftes interaktives Terminal. Es wird keine eigene Persistenz eingerichtet. Firmware-Updates können die Methode ändern oder schließen. Die angezeigten Rückgabewerte und Cleanup-Felder beachten.

## 7. XMB-Launcher installieren

Das Schwesterprojekt tizen-deck-xmb lokal bauen. Reguläre SDB-Installation zuerst versuchen. Der explizite Root-Weg lautet dort:

```sh
bash install.sh TV_IP --root-deck /absoluter/pfad/tizen-deck
```

Tizen Deck muss vorher für dieses Gerät mit `--root-support` eingerichtet sein. Es wird nur das ausgewählte Paket installiert, keine weiteren Apps entfernt. App-ID: `TizenDeckX.XMB`. Start über die SDK-Konsole: `app_launcher -s TizenDeckX.XMB`.

## 8. Backups und Firmware

Vor Änderungen persönliche App-Dateien separat sichern und SHA-256 bilden. Ein Archiv eines App-Verzeichnisses ist kein signiertes Reinstallationspaket. Ein laufender eMMC-Dump kann wechselnde Schreibpartitionen enthalten und ist nicht transaktional konsistent. Separate eMMC-Bootbereiche gehören nicht zum Hauptgerät; RPMB, OTP, Secure-Element und weitere Controller können fehlen. Ein Dump ist ohne getesteten Wiederherstellungsweg kein garantiertes Unbrick-Backup.

Keine universellen `dd`-Befehle gegen erratene Gerätedateien verwenden. Erst `/proc/partitions`, `/proc/mtd`, `lsblk -b` und die Mounts lesen; exakte Größe, Modell und Device dokumentieren. Nur lesen, Empfangsgröße und Stream-/Datei-Hashes prüfen. Niemals `of=/dev/...` bei einer Sicherung verwenden. Dieses Repo automatisiert absichtlich keinen modellunabhängigen Firmware-Flash. Dumps enthalten möglicherweise Konten und Tokens und bleiben lokal außerhalb von Git.

## 9. Updates und Entfernung

Eigene App-Updates mit demselben Autorenschlüssel signieren. `.signing/` separat privat sichern. Im Dashboard schaltet „Automatische Updates“ ausschließlich den getesteten Firmware-Autoupdate-Schalter; es entfernt den Updater nicht. Ein genereller Update-Stopp ist keine Voraussetzung für dieses Projekt.

Launcher entfernen (bewusste Deinstallation): `pkgcmd -u -n TizenDeckX`. Webserver mit Strg+C stoppen; danach kann sein lokaler Projektordner entfernt werden. Dadurch wird nichts an der Firmware zurückgeflasht. Persönliche Tokens und `.state/` nicht veröffentlichen.

## Quellen

- https://developer.samsung.com/smarttv/develop/getting-started/using-sdk/tv-device.html
- https://developer.samsung.com/smarttv/develop/api-references/tizen-web-device-api-references/application-api.html
- https://github.com/chris-ritsen/samsung-tv-root

Modellspezifische Root-Schritte: [ROOT-LS32BM700UUXEN.md](ROOT-LS32BM700UUXEN.md). Entwicklungsanteile: [PROVENANCE.md](PROVENANCE.md).
