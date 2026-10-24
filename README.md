# Tizen Deck

Lokale, passwortgeschützte Webverwaltung für Samsung-Tizen-Monitore: Status, Prozesse, Dienste, Pakete, Netzwerk, Fernbedienung und Konsole. Root-Befehle sind ausdrücklich wählbar und verwenden eine temporäre Sitzung.

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

## Getestet

Samsung LS32BM700UUXEN, T-NKLBWWC-1720.7, Tizen 6.5, ARMv7, .NET 3.1. Keine allgemeine Kompatibilitätszusage für andere Modelle. Web-Login und Live-Messungen sowie temporärer Root-Zugriff wurden an diesem Gerät geprüft. Automatisierte Tests prüfen Zugriffsschutz, CSRF und Auftragsverarbeitung. Fernbedienungs-WebSocket benötigt die Zustimmung am TV; Bildschirm-Livebild wird nicht unterstützt.

Tests: `.venv/bin/python -m unittest discover -s tests -v`.

## Abhängigkeiten

Root-/SDK-Brücke: [samsung-tv-root](https://github.com/chris-ritsen/samsung-tv-root), Commit `3b97edf3762db7670585d73e29623dc56195bf98`, Unlicense. SDB und Root-Release werden heruntergeladen und per SHA-256 geprüft. Sie sind nicht Teil dieses Repositorys. Private Zustände, Tokens, Dumps und Autorenschlüssel gehören niemals ins Repository.
