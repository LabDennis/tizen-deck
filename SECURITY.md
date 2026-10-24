# Zugriff und Grenzen

Die Anwendung stellt eine bewusst mächtige Konsole für den eigenen Monitor bereit. Nur 127.0.0.1 ist freigegeben. HTTP auf Loopback, scrypt-Passworthash, HttpOnly/SameSite-Cookie, Host-/Origin-Prüfung und CSRF-Schutz; kein LAN-/Internet-Dienst.

SDK/SDB-Verbindungen sind gerätebedingt unverschlüsselt. Die temporäre Root-Brücke stammt aus dem separat geladenen Upstream-Projekt. Die Fernbedienung nutzt TLS mit dem selbstsignierten TV-Zertifikat; dessen CA-Verifikation ist in diesem LAN-Client deaktiviert. Deshalb nur in einem vertrauenswürdigen, beschränkten lokalen Netz verwenden.

`.state/` enthält Passwortmaterial, Host-Konfiguration und Pairing-Token. `.deps/` enthält Drittwerkzeuge. Beide gehören nicht in Git. Dasselbe gilt für Firmware-Dumps, Signierschlüssel und Gerätebackups. Root-Zugriff ist nicht dauerhaft und hebt SMACK/Secure Boot nicht automatisch auf.

Die Anwendung löscht keine Samsung-Pakete automatisch. Die Root-Konsole kann bei absichtlicher Eingabe dennoch Daten löschen oder das Gerät beschädigen. Nicht als Mehrbenutzerdienst betreiben.
