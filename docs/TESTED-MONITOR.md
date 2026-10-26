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

## Backend-Updates 1.8.2

- Getrennte signierte Updates für Launcher und TV-Dienst im TV-Menü.
- Übertragung über den bereits angemeldeten HTTPS-Dienst; privater Zustand und
  eingeschriebener Signaturschlüssel bleiben erhalten.
- Ein absichtlich nicht startender Deck-Dienst wurde am Gerät erkannt und durch
  den gesicherten Vorgänger ersetzt. Der erste Fehlertest deckte einen falschen
  Erfolgsstatus des Samsung-.NET-Aufrufs auf; die korrigierte Implementierung
  prüft deshalb die tatsächliche HTTP-Antwort und Versionsnummer.
- Reguläres Backend-Update 1.8.1 → 1.8.2 und Launcher 1.7.2 → 1.8.2 installiert.
- Konfigurierbarer HTTPS-Release-Ordner vorhanden; noch keine reale Feed-Adresse
  eingerichtet. Weitere Details und Grenzen: [BACKEND-UPDATES.md](BACKEND-UPDATES.md).
- Die neue Boot-/Root-Forschung bleibt pausiert. Kein neues Boot-Verfahren
  eingerichtet und keine Firmware geflasht.


## Launcher-Leistung 1.8.5

Am 26.10.2026 installiert und mit dem Quellstand per SHA-256 abgeglichen.
Bei 600 horizontalen bzw. 280 vertikalen synthetischen Eingaben im sparsamen
Profil mit Menütönen sank das mittlere requestAnimationFrame-Intervall gegenüber
1.8.3 von 47,48 auf 40,09 ms bzw. von 23,28 auf 17,47 ms. Im Leerlauf rund 16,8 ms.
Browser-Zeichenintervalle sind kein direkter Panel-FPS-Nachweis. Temporärer
Messcode wurde danach entfernt. Die bisherigen Scrollanimationen bleiben erhalten.
Der signierte Launcher-Rückfall ist 1.8.3; der native Dienst bleibt 1.8.2.

Uninstrumentierter Gerätetest danach: 4.000 echte Richtungseingaben in 20 Serien,
anschließend zwei Minuten Ruhe (insgesamt ca. 10,7 Minuten). Derselbe
Launcher-Prozess blieb aktiv, der Wächter erkannte keinen Absturz. PSS stieg von
67,4 MiB auf maximal 105,6 MiB, fiel zwischendurch auf 84,2 MiB und lag am Ende bei
92,8 MiB. Die abschließende Bildschirmaufnahme zeigte das normale Menü. Der
verbleibende Speicheranstieg ist weiterhin offen; ein vollständig behobener
RAM-Fehler ist damit nicht nachgewiesen.

## Lokal vorbereitet: Launcher 1.9.0 (26.10.2026)

Der Monitor wurde über den vorhandenen Adminzugang ausgeschaltet. Die Display-API
bestätigte Bildschirmstatus 0; später war der native HTTPS-Dienst im Standby nicht
mehr erreichbar. Für die folgenden Arbeiten wurde kein Weckpaket gesendet.
Installiert bleiben Launcher **1.8.5**, Rückfall **1.8.3** und TV-Dienst **1.8.2**.

Ein passendes 1.9.0-Paket ist lokal signiert und seine Update-Signatur geprüft.
Es wurde weder auf dem Monitor bereitgestellt noch installiert.

- Lokaler Fehlernachweis: unsichtbare Kategorien und lange Eintragslisten konnten
  das Dokument über den Bildschirm hinaus vergrößern. Fokus auf die letzte
  Kategorie verschob im Browser die gesamte Seite um 211 Pixel. Feste
  Bildschirmgrenzen und nicht fokussierbare unsichtbare Kategorien verhindern
  dies im Regressionstest, einschließlich 192 schneller Eingaben.
- Audio-/Uhr-Refactor, Freigabe beendeter Audiostimmen, Schließen des Audiokontexts
  bei Ruhe und begrenzte native Statusanfragen sind automatisiert geprüft.
- Extras → Uhr & Timer ergänzt eine dunkle Uhr, Stoppuhr und Countdown. Kein
  Wecken aus Standby; Countdown-Signal nur während Deck läuft bzw. bei Rückkehr.
- Neun Unit- und neun Browser-Tests bestanden. Keine neue TV-FPS- oder RAM-Messung.

Der vom Nutzer gemeldete Bildfehler und der verbleibende Speicheranstieg sind erst
nach Installation und erneuter Prüfung auf der Tizen-Webengine abschließend
bewertbar. Keine Firmware- oder Boot-Änderung in diesem Arbeitsschritt.
