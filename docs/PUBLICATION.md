# Prüfung vor Veröffentlichung 1.0 — 29.10.2026

Geprüft wurden beide GitHub-Repositories einschließlich aller erreichbaren
Git-Objekte und Commit-Metadaten: 156 historische Dateiobjekte in tizen-deck,
246 in tizen-deck-xmb. Ergänzend wurden die Änderungen für 1.0 geprüft.

- Gitleaks über die gesamte Historie: keine bestätigten Secrets. Sieben
  Meldungen in tests/native-backend/Program.cs betreffen Firmware-Beispielwerte
  hinter dem Funktionsargument `key`, keine Zugangsschlüssel.
- Zusätzliche Suche nach privaten IPs, MACs, E-Mail-Adressen, lokalen
  Benutzerpfaden, Schlüsselblöcken und Zugangsdaten: Treffer waren Testdaten
  und Beispiele. Commit-E-Mail-Adressen verwenden GitHubs noreply-Domain.
- Keine weiteren GitHub-Branches oder Tags; keine Releases/Assets, Issues,
  Pull Requests oder Actions-Läufe. Wiki und Pages waren deaktiviert.
- Gerätekonfiguration, Signierschlüssel, Dumps und private Backup-Protokolle
  werden nicht mit veröffentlicht. Debloat-Dateien enthalten nur Befehle.
- 26 Python-Tests und 12 JavaScript-Testdateien bestanden. TV-Dienst 1.0.0
  erfolgreich gebaut. Debloat-/Privacy-Shellsyntax und Vorschau geprüft.

Kein vollständiges Sicherheitsgutachten und keine Garantie gegen jede Form
privater Information. Keine erneute TV-Installation, kein Hardwaretest und
keine erneute Ausführung der Debloat-Befehle im Rahmen dieser Veröffentlichung.
Die früheren Hardwareberichte sind historische Befunde, keine Tests von 1.0.
