"""Wake the configured monitor from an external host; never toggle its power."""
import ipaddress
import json
import os
import re
import socket
import time


def validate(mac, broadcast):
    if not isinstance(mac, str) or not re.fullmatch(r'(?:[0-9a-fA-F]{2}:){5}[0-9a-fA-F]{2}', mac):
        raise ValueError('WLAN-/LAN-MAC als sechs Hex-Paare eingeben, z. B. 02:00:00:00:00:01.')
    raw = bytes.fromhex(mac.replace(':', ''))
    if raw == bytes(6) or raw[0] & 1:
        raise ValueError('Eine gültige Geräte-MAC verwenden.')
    if not isinstance(broadcast, str):
        raise ValueError('Gültige IPv4-Broadcast-Adresse eingeben.')
    try:
        address = ipaddress.IPv4Address(broadcast)
    except (ValueError, TypeError):
        raise ValueError('Gültige IPv4-Broadcast-Adresse eingeben.') from None
    if address.is_loopback or address.is_multicast or address.is_unspecified:
        raise ValueError('Broadcast-Adresse des Monitor-Netzes verwenden.')
    return {'mac': mac.upper(), 'broadcast': str(address)}


class WakeOnLan:
    def __init__(self, state):
        self.path = state / 'wake.json'

    def config(self):
        try:
            data = json.loads(self.path.read_text())
            return validate(data.get('mac'), data.get('broadcast'))
        except (OSError, ValueError, AttributeError):
            return {'mac': '', 'broadcast': '255.255.255.255'}

    def save(self, mac, broadcast):
        data = validate(mac, broadcast)
        temp = self.path.with_suffix('.new')
        temp.write_text(json.dumps(data))
        temp.chmod(0o600)
        os.replace(temp, self.path)
        return data

    def wake(self, device, timeout=20):
        data = self.config()
        if not data['mac']:
            raise ValueError('Zuerst unter Einstellungen die Wake-on-LAN-Adresse speichern.')
        def awake():
            try:
                return str(device().get('PowerState', '')).lower() == 'on'
            except Exception:
                return False
        if awake():
            return {'awake': True, 'sent': False, 'message': 'Monitor ist bereits eingeschaltet.'}
        packet = b'\xff' * 6 + bytes.fromhex(data['mac'].replace(':', '')) * 16
        with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as sock:
            sock.setsockopt(socket.SOL_SOCKET, socket.SO_BROADCAST, 1)
            for _ in range(3):
                sock.sendto(packet, (data['broadcast'], 9))
                time.sleep(.1)
        deadline = time.monotonic() + timeout
        while time.monotonic() < deadline:
            if awake():
                return {'awake': True, 'sent': True, 'message': 'Monitor ist eingeschaltet und antwortet.'}
            time.sleep(1)
        return {'awake': False, 'sent': True,
                'message': 'Wecksignal gesendet; Einschalten noch nicht bestätigt. „Mit Mobilgerät einschalten“, MAC und Broadcast-Netz prüfen.'}
