# Private Updates vom eigenen PC

Geprüfter Stand: DeckTV 1.9.1 und Launcher 1.11.3. Der PC stellt signierte Pakete über einen
kleinen HTTPS-Dienst bereit. Updates werden ausschließlich auf Anforderung geladen
und erst nach einer weiteren Auswahl installiert. Der Monitor braucht den bereits
laufenden nativen Deck-Dienst. Dies richtet keinen Root-Zugang nach Kaltstart ein.

## Einrichtung auf dem Linux-PC

Python 3.12+, OpenSSL und systemd als Benutzerdienst werden benötigt. In diesem
Repository ausführen; die Beispieladressen durch die eigenen Geräte ersetzen:

```sh
./install-release-server.sh 192.0.2.20
python3 enroll_release_feed.py --url https://192.0.2.30:8843 \
  --ca .state/tv-service/server.crt
```

Das zweite Kommando fragt nach dem vorhandenen TV-Webserver-Passwort. Es hinterlegt
die konkrete TLS-Zertifikatidentität des PCs und einen eigenen zufälligen
Lesezugang im Monitor. Beide Werte werden nicht ausgegeben. Ein Wechsel der
Updateadresse im TV-Menü entfernt diese Bindung und Zugangsdaten; anschließend
am PC erneut anmelden/einrichten. Das lokale TLS-Zertifikat gilt 825 Tage und muss
vor Ablauf kontrolliert erneuert und erneut hinterlegt werden.

Bei UFW nur die benötigte Verbindung erlauben, Beispiel:

```sh
sudo ufw allow in on wlan0 from 192.0.2.30 to 192.0.2.20 port 8845 proto tcp comment 'Tizen Deck Updates'
```

Der Dienst bindet nur die angegebene PC-IP. Diese sollte im DHCP dauerhaft
zugeordnet sein. Er läuft als normaler Benutzer und startet bei dessen Anmeldung.
Wenn der PC schläft, ausgeschaltet oder der Benutzer abgemeldet ist, kann die
Quelle nicht verfügbar sein. Der TV zeigt dann einen Fehler; installierte Apps
und zuvor geprüfte Updatepakete bleiben erhalten. Es gibt keine Router- oder
Internet-Portfreigabe durch den Installer.

## Neue Pakete bereitstellen

Die vorhandenen, auf dem Monitor hinterlegten Signierschlüssel verwenden:

```sh
# TV-Dienst bauen/signieren
DOTNET_BIN=/path/to/dotnet bash tv-service/build.sh
python3 tv-service/prepare_backend.py
python3 release_server.py publish .state/backend-pending

# Gerätespezifischen Launcher lokal bauen/signieren, dann:
python3 tv-service/prepare_update.py /path/to/TizenDeckX.wgt
python3 release_server.py publish .state/tv-service/pending-update
```

Den Launcher wie in dessen Build-Anleitung mit seiner privaten `device-config.js`
erstellen. Die WGT enthält gerätespezifische Konfiguration und gehört **nicht in
öffentliche Releases**. Der private Feed prüft Signatur und Hash vor der
Bereitstellung. Seine Verzeichnisse enthalten ausschließlich die freigegebenen
Paketdateien; Zugangsdaten, Schlüssel und Dateilisten sind nicht abrufbar.

Im TV: **Einstellungen → System → Tizen-Deck-Updates**. Unter **Launcher-Updates**
oder **TV-Dienst aktualisieren** zunächst **Vom PC laden und prüfen** wählen,
danach gegebenenfalls die neuere Version installieren. Vorhandene Rückfallpakete
bleiben über die jeweiligen Menüs erreichbar. Gleiche/ältere Pakete können geladen,
aber nicht als normales Upgrade installiert werden.

Auf dem Samsung M7 LS32BM700UUXEN mit T-NKLBWWC-1720.7 wurden am 26.10.2026
beide Komponenten über diesen Feed geladen, signiert geprüft und installiert.
Launcher 1.11.3 fokussiert bei bereits eingerichteter Quelle den Downloadknopf;
der Download per Fernbedienung wurde am Monitor samt Erfolgsmeldung geprüft.
Die installierten Launcherdateien stimmen mit den lokalen Quellen überein.
Als signierte Rückfallpakete sind Launcher 1.11.2 und Dienst 1.9.0 gespeichert.

## Betrieb und Entfernen

```sh
systemctl --user status tizen-deck-updates.service
systemctl --user restart tizen-deck-updates.service
# Deaktivieren:
systemctl --user disable --now tizen-deck-updates.service
```

Private Daten liegen unter `.state/release-server/`; niemals in Git einchecken.
Die Paketablage hält veröffentlichte Stände vor. `current.json` zeigt auf die
aktuellen Pakete. Alte, nicht referenzierte Paketverzeichnisse können manuell
archiviert werden. Ein Publish ersetzt nur den Verweis und lässt alte Pakete
unverändert; ein zeitgleich laufender Download wird bei einem Versionsmix über
Signatur/Hash abgewiesen und kann erneut gestartet werden.

Der Feed verwendet `/backend/{manifest.json,manifest.sig,backend.zip}` und
`/launcher/{manifest.json,manifest.sig,launcher.wgt}` unter der konfigurierten
Basisadresse. Es gibt keine Weiterleitungen und keine Schreib-API. Downloads
sind auf 25 Sekunden und 16 MiB begrenzt. Die TLS-Bindung ersetzt bei der lokalen
Quelle die öffentliche Zertifikatskette; ein anderer oder abgelaufener Schlüssel
wird abgelehnt. Ohne hinterlegte Bindung gilt die normale TLS-Vertrauensprüfung.
