# Debloat und Datenschutz am Samsung M7

Diese Skripte enthalten die am LS32BM700UUXEN mit T-NKLBWWC-1720.7
verwendeten Befehle vom 25.10.2026, zusammengeführt aus den lokalen
Befehlsprotokollen. Neu sind Vorschau-/Ausführungsschutz und der Abbruch bei
bereits vorhandenem Phase-2-Backup. Sie werden nicht vom Installer gestartet.
Die zusammengestellte Fassung wurde auf Shell-Syntax und Vorschau geprüft,
nicht erneut auf einem TV ausgeführt. Keine Zusage für andere Firmware/Modelle.

1. Skripte lesen und den eigenen Wiederherstellungsweg klären.
2. Auf den bereits gerooteten TV übertragen. Ohne Argument verändern sie nichts.
3. Optional `sh privacy-apply.sh --apply` für die ursprünglichen Datenschutz-
   Dienstsperren ausführen. Das Skript legt einen Rücknahmehelfer an.
4. `sh debloat.sh --apply` führt Paket-Backups, Deinstallationen und das
   nachträgliche Service-Cleanup aus. Nur einzeln und bewusst ausführen.

Entfernt werden die damals ausgewählten Pakete für Samsung Analytics,
Context Awareness, TV Plus, Bixby/Alexa/Sprachsteuerung, SmartThings und
Samsung Health. Firmware-Autoupdate wird ausgeschaltet. WLAN und Bluetooth
sind nicht Teil der Deinstallationsliste. Das ist keine Garantie, dass keine
Telemetrie mehr übertragen wird.

Die Sicherungen bleiben unter `/opt/usr/share/codex-privacy-20261025/` auf
dem Gerät. Sie können private Daten und Samsung-Dateien enthalten: nicht ins
Repository oder in öffentliche Issues hochladen. Die Verzeichnisarchive sind
keine vollständige, automatisch wiederinstallierbare Paketsicherung. Bei
Fehlern stoppt das Skript; bereits entfernte Pakete bleiben entfernt.
Ein vollständiger automatischer Rollback des Debloat ist nicht implementiert.
Der Rücknahmehelfer des Datenschutzskripts betrifft nur dessen Dienstsperren,
nicht die Paketentfernungen, Autoupdate oder die späteren Debloat-Marker.

Die aktuellen Datenschutzschalter des TV-Dienstes sind separat implementiert;
sie sind kein Ersatz für die Wiederherstellung deinstallierter Pakete.
