# Entstehung und Quellen

Tizen Deck und Tizen Deck XMB wurden im Oktober 2026 von **LabDennis gemeinsam
mit OpenAI Codex** entwickelt. Codex hat auf Anweisung des Eigentümers den
projektspezifischen Code, Installer, Tests und Dokumentation in dieser Sitzung
maßgeblich erstellt und überarbeitet. LabDennis hat Anforderungen und Gestaltung
vorgegeben, sein eigenes Gerät bereitgestellt und Funktionen am Monitor bestätigt.
Das ist eine transparente Beschreibung der Arbeitsweise, kein Nachweis, dass jede
Funktion vollständig getestet ist. Den Prüfstand nennt TESTED-MONITOR.md.

## Projektspezifische Teile

- XMB-inspirierter HTML/CSS/JavaScript-Launcher mit eigenen Symbolen und Tönen.
- Passwortgeschützte externe Python-Webverwaltung und optionale native
  .NET-TV-Verwaltung; Bildschirmaufnahmen, Diagnose, kontrollierte Eingaben.
- Docker-Paket, Gerätekonfiguration, Installationsskripte, lokale signierte
  Launcher-Updates und begleitende Tests.

## Bestehende Arbeiten und Laufzeiten

- [chris-ritsen/samsung-tv-root](https://github.com/chris-ritsen/samsung-tv-root):
  SDB-/Root-Kette und Payloads, verwendet über den festgelegten Commit
  `3b97edf3762db7670585d73e29623dc56195bf98` und Release `v0.0.12`.
  Upstream-Lizenz: Unlicense. Der Exploit wird nicht als eigene Entdeckung beansprucht.
- Samsung/Tizen: vorhandene Firmware, Laufzeiten, System-APIs, SDB und offizieller
  Zertifikatgenerator. Werkzeuge werden separat geladen; Samsung-Systemdateien
  und Firmware-Dumps werden nicht mit diesen Repos verteilt.
- [samsung-tv-ws-api](https://github.com/xchwarze/samsung-tv-ws-api): Referenz für
  Samsungs Remote-Protokoll; keine Behauptung einer eigenen Protokollerfindung.
- [gtors/vdfs-unpack](https://github.com/gtors/vdfs-unpack) und
  [vdfs-team/vdfs4](https://github.com/vdfs-team/vdfs4): Referenzen und Werkzeuge
  für die private Offline-Analyse des eigenen Firmware-Dumps. Die lokale
  Extraktorkorrektur gehört nicht zum auf dem Monitor installierten Launcher.
- Plex, Jellyfin und Moonlight sind eigenständige Drittprojekte. Verknüpfungen im
  Launcher machen diese Apps nicht zu Eigenentwicklungen von Tizen Deck.

Die Gestaltung ist von der PS3-XMB-Oberfläche inspiriert. Es wurden keine Sony-
Firmware, Originalsounds oder aus Sony-Firmware extrahierte Assets eingebaut.
Es besteht keine Verbindung oder offizielle Unterstützung durch Samsung oder Sony.
