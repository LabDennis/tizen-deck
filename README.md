# Tizen Deck

Weboberfläche zum Verwalten von Samsung-Tizen-TVs und -Monitoren. Zeigt den
Systemstatus, steuert Apps und Lautstärke und bietet Fernbedienung, Screenshots,
Wake-on-LAN und eine Konsole. Läuft auf dem PC oder optional direkt auf dem TV.
Dazu gibt es [Tizen Deck XMB](https://github.com/LabDennis/tizen-deck-xmb) als Launcher.

**Version 1.0 · Zum Großteil mit OpenAI Codex gevibecodet.**

![Anmeldung zur Webverwaltung](docs/screenshots/login.png)

*Browser-Vorschau.*

## Installation (Linux x86_64)

Voraussetzungen: Python 3.12+, Git, curl, Netzwerk zum eigenen Monitor.

```sh
git clone https://github.com/LabDennis/tizen-deck.git
cd tizen-deck
bash install.sh 192.0.2.10
bash start.sh
```

`192.0.2.10` durch die TV-IP ersetzen. Öffnen: http://localhost:8765. Benutzer `admin`; das beim ersten Start erzeugte Passwort liegt ausschließlich lokal in `.state/initial-login.txt`. Nach der Anmeldung in Einstellungen ändern.

Optional mit Root-Unterstützung:

```sh
bash install.sh 192.0.2.10 --root-support
```

Der Installer lädt Werkzeuge herunter und konfiguriert nur den PC. Er führt keinen Root-Vorgang auf dem TV aus. Erst eine ausdrücklich gewählte Root-Aktion baut eine temporäre Sitzung auf. Kein Boot-Hook, kein Firewall-Umbau, kein dauerhafter Root-Dienst.

## Anleitung

[Installation, Entwicklermodus, Netzwerk, Root, Backup und Entfernung](docs/GUIDE.md).

[PC-Fernbedienung und Einschalten aus Standby](docs/PC-REMOTE.md): `python3 pc_remote.py`
öffnet eine interaktive CLI mit Pfeiltasten, OK und Zurück. Einzelbefehle,
PNG-Aufnahmen und Wake-on-LAN über die externe Bridge sind ebenfalls verfügbar.
Das vorhandene Webinterface bleibt als Browser-Fernbedienung nutzbar.

[Deck als Standard-Home, Autostart und Rückweg zu Samsung](docs/DEFAULT-HOME.md):
optional, separat eingerichtet und auf dem M7 geprüft.

[App-Sitzungen automatisch schließen](docs/APP-LIFECYCLE.md): Standardoption im
Launcher, mit laufendem nativen TV-Dienst.

[Aktueller Systemausbau](docs/TV-SERVICE.md): native Audiobedienung, Netzstatus,
rückgängig machbare Datenschutz-Sperren, signierte Launcher- und Backend-Updates mit Rückfall
und begrenzte Absturzbehandlung. [Geprüfte Grenzen der Geräteeinstellungen](docs/DEVICE-SETTINGS-STATUS.md).
[Stand der Boot-Untersuchung](docs/BOOT-RESEARCH.md).

## Getestet

Samsung LS32BM700UUXEN, T-NKLBWWC-1720.7, Tizen 6.5, ARMv7, .NET 3.1. Keine allgemeine Kompatibilitätszusage für andere Modelle. Web-Login und Live-Messungen sowie temporärer Root-Zugriff wurden an diesem Gerät geprüft. Automatisierte Tests prüfen Zugriffsschutz, CSRF und Auftragsverarbeitung. Fernbedienungs-WebSocket benötigt die Zustimmung am TV. Echte PNG-Bildschirmaufnahmen werden unterstützt; flüssiges Video-Streaming ist nicht implementiert.

Tests: `.venv/bin/python -m unittest discover -s tests -v`.

## Abhängigkeiten

Root-/SDK-Brücke: [samsung-tv-root](https://github.com/chris-ritsen/samsung-tv-root), Commit `3b97edf3762db7670585d73e29623dc56195bf98`, Unlicense. SDB und Root-Release werden heruntergeladen und per SHA-256 geprüft. Sie sind nicht Teil dieses Repositorys. Private Zustände, Tokens, Dumps und Autorenschlüssel gehören niemals ins Repository.

## Weitere Infos

- [Nativer TV-Dienst und Updates](docs/TV-SERVICE.md)
- [Debloat-Skripte](docs/DEBLOAT.md)
- [Getestetes Gerät und bekannte Grenzen](docs/TESTED-MONITOR.md)
- [Quellen und Drittprojekte](docs/PROVENANCE.md)
- [Entwicklungsnotizen](docs/DEVELOPMENT.md)

Der native TV-Dienst muss nach einem Kaltstart erneut aktiviert werden.
Die öffentliche 1.0.0 liegt unter den früheren Entwicklungsnummern; der normale
Updater bietet deshalb keinen Wechsel von diesen Versionen auf 1.0.0 an.
