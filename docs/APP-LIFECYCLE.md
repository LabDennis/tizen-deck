# App-Sitzungen automatisch schließen

Launcher 1.6.0 bietet **Einstellungen → Apps im Hintergrund**. Die Option ist
standardmäßig eingeschaltet. Bei Rückkehr zum sichtbaren Deck-Menü sowie vor
dem nächsten App-Start fordert der Launcher den nativen TV-Dienst zum Aufräumen
auf. Für Musik im Hintergrund lässt sich die Option ausschalten.

Betroffen sind ausschließlich die fest eingetragenen Apps Plex, Jellyfin,
YouTube, Moonlight, Browser, Bildschirmspiegelung und Remote Workspace. Die als
Nächstes ausgewählte App bleibt erhalten. Samsung Home, TV-Viewer, Einstellungen,
Bluetooth-Liste, Deck selbst und sonstige Systemdienste werden nicht beendet.
Eine Wiedergabe oder ungespeicherte App-Sitzung kann durch das Schließen enden.

Der Dienst prüft den tatsächlichen Vordergrundprozess vor dem ersten und vor
jedem weiteren Schließaufruf. Wechselt der Fokus, stoppt die Bereinigung. Er nutzt
`app_launcher -t` zum regulären Beenden und prüft danach die App-PID. Ein noch
laufender Prozess wird nicht als geschlossen gemeldet. Der lokale Endpunkt
`POST /apps/cleanup` ist durch den vorhandenen privaten Control-Token geschützt;
er akzeptiert keine frei wählbaren Befehle oder zusätzlichen App-IDs.

## Samsungs Reserveprozesse

Auf dem M7 startet Samsung den Browser nach Beenden erneut im Vorlademodus.
`com.samsung/featureconf/broswer_preloading` stand im Test auf `1`; dieser Wert
wurde nicht verändert. Die Diensthilfe erlaubt seine Änderung nur für Debug-
Images. Ein neuer PID ist deshalb separat als `restarted` erfasst. Deck merkt
sich diese Reserve für die Laufzeit des Diensts und beendet sie nicht bei jeder
Rückkehr wieder, auch wenn sie erst nach Abschluss des Schließaufrufs erscheint.
Ein erneuter Browserstart **über Deck** hebt diese Ausnahme für
die nächste Benutzersitzung auf. Starts außerhalb Decks können dieser Zuordnung
entgehen. Das ist kein Versprechen, sämtliche Browserprozesse oder deren RAM zu
entfernen.

## Voraussetzungen und Tests

Der native TV-Dienst muss laufen. Ist er nicht erreichbar, startet die App
weiterhin und der Launcher meldet, dass ohne Aufräumen gestartet wurde. Die
Option installiert keinen Boot-Hook: Nach vollständigem Monitor-Neustart braucht
der native Dienst weiterhin eine separate Aktivierung. Standby ist davon zu
unterscheiden. Die externe Docker-/Python-Bridge stellt diesen lokalen
Launcher-Endpunkt derzeit nicht bereit.

`dotnet run --project tests/native-apps/Tests.csproj` prüft App-Auswahl,
Zielausnahme, Samsung-Fallback, Fokuswechsel, fehlgeschlagenes Schließen sowie den
Umgang mit Reserveprozessen. Die Launcher-Tests `apps.test.js` und
`inputs.browser.js` prüfen Standardzustand, Opt-out, parallele Startanforderungen,
Dienstfehler, Eingangswahl und Fernbedienungsfokus.

Am Monitor mit Launcher 1.6.0 geprüft: Browser über Deck öffnen, Home drücken,
alte Browser-PID beendet und neue Reserve-PID erkannt. Zwei anschließende
Bereinigungen ließen diese Reserve unverändert. Ihre RSS lag bei etwa 35 MiB
gegenüber 96 MiB der geöffneten Browser-Sitzung; das ist eine Momentaufnahme,
keine zugesicherte Einsparung. Jellyfin wurde über Deck geöffnet und nach Home
automatisch beendet (`app_launcher -r`: `not running`, alte PID verschwunden).
Weitere Apps der Liste wurden in diesem Durchlauf nicht einzeln am Gerät getestet.
