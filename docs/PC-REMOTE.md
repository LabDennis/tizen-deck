# PC-Fernbedienung

`pc_remote.py` steuert den Monitor über einen vorhandenen, angemeldeten Deck-Webserver.
Python 3.9+ genügt; das Skript benötigt keine zusätzlichen Pakete und installiert
keinen Dienst. Linux wurde am Samsung M7 geprüft. Der Windows-Tastaturpfad ist
vorbereitet, aber noch nicht an einem Windows-PC geprüft.

## Start

Mit der externen Bridge auf diesem PC:

```sh
python3 pc_remote.py
```

Direkt mit dem nativen HTTPS-Dienst auf dem Monitor:

```sh
python3 pc_remote.py --url https://192.0.2.10:8843 --ca .state/tv-service/server.crt
```

IP ersetzen; das Zertifikat stammt aus der eigenen Deck-Installation. Für einen
anderen PC das öffentliche `server.crt` kopieren, niemals `server.key` oder den
Control-Token. Das Passwort wird verdeckt abgefragt. `--user NAME` wählt einen
anderen Benutzer. Passwörter und Sitzungscookies werden nicht gespeichert;
beim regulären Beenden wird die Sitzung abgemeldet.

## Bedienung

| Taste am PC | Aktion am Monitor |
| --- | --- |
| Pfeile oder WASD | Navigation |
| Enter oder Leertaste | OK; bei Flappy starten/fliegen |
| Escape, Backspace oder B | Zurück |
| H / M | Home / Menü |
| + / − / 0 | Lauter / leiser / stumm |
| P | Wiedergabe |
| Q oder Strg+C | CLI beenden |

Die Eingabe gilt nur im aktiven Terminal. Auf den Monitor schauen; die CLI ist
kein Videostream. Netzwerk und TV-Eingabeverarbeitung können das Spiel verzögern.
Die vorhandene Browser-Fernbedienung bleibt unter `/#remote` verfügbar.

## Einzelbefehle

```sh
python3 pc_remote.py key ok
python3 pc_remote.py key home
python3 pc_remote.py status
python3 pc_remote.py capture screen.png
python3 pc_remote.py wake
python3 pc_remote.py text 'Suchbegriff'
```

Optionen wie `--url`, `--ca` und `--user` stehen vor dem Befehl. Jeder Aufruf fragt
das Passwort ab. Eine Aufnahme überschreibt keine bestehende Datei. Der native
TV-Dienst unterstützt noch keine freie Texteingabe; dort mit Pfeiltasten und OK
die Bildschirmtastatur verwenden. Bei der externen Bridge kann der Monitor beim
ersten Tastendruck die Freigabe für „Tizen Deck“ verlangen.

## Aus Standby einschalten

`wake` verwendet den **externen Deck-Server**. Dieser muss auf einem laufenden PC
oder Docker-Host erreichbar sein. Unter Einstellungen → Wake-on-LAN einmal MAC
und Broadcast-Adresse des Monitor-Netzes hinterlegen. Im Webinterface erscheint
auf der Übersicht außerdem „Monitor einschalten“.

Auf dem Monitor muss „Mit Mobilgerät einschalten“ aktiviert sein. Der Sender muss
das Broadcast-Netz erreichen; VLANs benötigen gegebenenfalls einen gezielt
konfigurierten Relay. Das Programm ändert keine Router- oder Firewall-Regeln.
Docker verwendet dafür das vorhandene Linux-Hostnetzwerk. Für Zugriff von einem
anderen PC auf die nur lokal gebundene externe Bridge eignet sich ein SSH-Tunnel.

Der native Webserver schläft mit dem Monitor und kann währenddessen keine
Weckanfrage empfangen. Wake-on-LAN ersetzt weder einen Kaltstart-Dienst noch eine
Stromversorgung. Es sendet keinen Power-Toggle: Ein bereits eingeschalteter
Monitor bleibt an. Erfolg wird erst gemeldet, wenn die Geräte-API „on“ bestätigt.

## Geprüft am LS32BM700UUXEN / Firmware 1720.7

Am 25.10.2026 nach normalem Standby: Nach 25 Sekunden waren Geräte-API,
nativer Webserver und SDB nicht mehr erreichbar. Nach 80 Sekunden wurde das
Wecksignal gesendet. Rund 21 Sekunden später meldete die API „on“ und alle drei
Dienste antworteten wieder. Kein Netztrennungs-/Kaltstarttest.

Samsung beschreibt die Voraussetzung unter
[Smart Monitor per Mobilgerät einschalten](https://www.samsung.com/au/support/displays/turn-on-samsung-smart-monitor-with-phone/).
Die [IP-Control-Dokumentation](https://image-us.samsung.com/SamsungUS/samsungbusiness/tv-ci-resources/Samsung-IP-Control.pdf)
erläutert, weshalb nach dem Einschlafen des IP-Servers ein Wake-Signal erforderlich ist.

## Geprüfter Launcher-Neustart

Mit DeckTV 1.9.1 über den nativen TV-Webserver:

```sh
python3 pc_remote.py --url https://TV_IP:8843 --ca .state/tv-service/server.crt restart
```

Startet ausschließlich Tizen Deck neu. Erfolg erfordert eine neue Prozess-ID,
eine frische Rückmeldung aus dem aufgebauten Menü und das fokussierte Fenster
`Tizen Deck XMB`. Ein reservierter Prozess oder die leere `file:///`-Seite reicht
nicht. Nach einem Zeitlimit wird Samsung Home als Rückfall angefordert, sofern
keine andere App aktiv ist; die CLI meldet dabei einen Fehler statt Erfolg.
Ein Neustart des Monitors selbst findet nicht statt.

Am 26.10.2026 auf dem genannten Monitor geprüft: reguläre Neustarts mit neuer
Prozess-ID und sichtbarem Menü sowie ein Fehlerlauf mit absichtlich leerer
Launcher-Seite. Der Fehlerlauf meldete `ready: false` und öffnete Samsung Home.
Danach wurde die eigene Originaldatei wiederhergestellt, per SHA-256 geprüft und
der Launcher erfolgreich neu gestartet. Fremde Apps und Samsung-Dateien wurden
für diesen Test nicht ersetzt.
