#!/usr/bin/env python3
"""PC remote for an existing Tizen Deck server. Python standard library only."""
import argparse
import getpass
import http.cookiejar
import ipaddress
import json
import ssl
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path


KEYS = {
    'up': 'KEY_UP', 'down': 'KEY_DOWN', 'left': 'KEY_LEFT', 'right': 'KEY_RIGHT',
    'ok': 'KEY_ENTER', 'back': 'KEY_RETURN', 'home': 'KEY_HOME', 'menu': 'KEY_MENU',
    'volup': 'KEY_VOLUP', 'voldown': 'KEY_VOLDOWN', 'mute': 'KEY_MUTE',
    'play': 'KEY_PLAY', 'pause': 'KEY_PAUSE', 'stop': 'KEY_STOP',
}


def server_url(value):
    p = urllib.parse.urlsplit(value)
    if p.scheme not in ('http', 'https') or not p.hostname or p.username or p.password or p.path not in ('', '/') or p.query or p.fragment:
        raise ValueError('Server-Adresse als https://HOST:PORT oder http://localhost:8765 angeben.')
    # Never send credentials over plain HTTP to a remote network host.
    loopback = p.hostname == 'localhost'
    try:
        loopback = loopback or ipaddress.ip_address(p.hostname).is_loopback
    except ValueError:
        pass
    if p.scheme == 'http' and not loopback:
        raise ValueError('HTTP nur für localhost verwenden; für entfernte Server HTTPS oder SSH-Tunnel nutzen.')
    _ = p.port  # Validate port before making a request.
    return value.rstrip('/')


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        raise ValueError('Unerwartete Weiterleitung; Server-Adresse prüfen.')


class Remote:
    def __init__(self, url, ca=None):
        self.url = server_url(url)
        self.csrf = ''
        self.session = {}
        self.cookies = http.cookiejar.CookieJar()
        context = ssl.create_default_context(cafile=ca)
        self.http = urllib.request.build_opener(
            NoRedirect(), urllib.request.HTTPCookieProcessor(self.cookies),
            urllib.request.HTTPSHandler(context=context))

    def request(self, path, data=None, binary=False):
        headers = {}
        body = None
        if data is not None:
            body = json.dumps(data).encode()
            headers = {'Content-Type': 'application/json', 'Origin': self.url,
                       'X-CSRF-Token': self.csrf}
        req = urllib.request.Request(self.url + path, data=body, headers=headers)
        try:
            with self.http.open(req, timeout=12) as response:
                content = response.read(9 * 1024 * 1024)
        except urllib.error.HTTPError as error:
            try:
                message = json.loads(error.read(4000)).get('error', 'Anfrage fehlgeschlagen')
            except (ValueError, AttributeError):
                message = 'Anfrage fehlgeschlagen'
            finally:
                error.close()
            raise ValueError(f'HTTP {error.code}: {message}') from None
        except urllib.error.URLError as error:
            if isinstance(error.reason, ssl.SSLCertVerificationError):
                raise ValueError('Zertifikat nicht vertrauenswürdig. Mit --ca das Zertifikat deiner Deck-Installation angeben.') from None
            raise ValueError('Server nicht erreichbar. Adresse, Webserver und Standby prüfen.') from None
        return content if binary else json.loads(content)

    def login(self, username, password):
        self.csrf = self.request('/api/login', {'username': username, 'password': password})['csrf']
        self.session = self.request('/api/session')

    def logout(self):
        if self.csrf:
            try:
                self.request('/api/logout', {})
            except (OSError, ValueError):
                pass
        self.csrf = ''
        self.cookies.clear()

    def job(self, path, data):
        result = self.request(path, data)
        if 'job' not in result:
            return result
        deadline = time.monotonic() + 120
        while time.monotonic() < deadline:
            status = self.request('/api/jobs/' + urllib.parse.quote(result['job'], safe=''))
            if status['status'] == 'done':
                return status['result']
            if status['status'] == 'error':
                raise ValueError(status.get('error', 'Auftrag fehlgeschlagen'))
            time.sleep(.25)
        raise ValueError('Zeitlimit beim Warten auf den Auftrag erreicht.')

    def key(self, name):
        if name not in KEYS:
            raise ValueError('Unbekannte Taste: ' + name)
        return self.request('/api/input', {'key': KEYS[name]})

    def wake(self):
        if self.session.get('mode') == 'tv':
            raise ValueError('Zum Aufwecken --url http://localhost:8765 verwenden. Der native TV-Webserver schläft mit dem Monitor.')
        return self.job('/api/wake', {})

    def capture(self, output):
        # Refuse to overwrite a file before starting a capture on the device.
        if output.exists():
            raise ValueError('Datei existiert bereits: ' + str(output))
        self.job('/api/capture', {})
        data = self.request('/api/screen', binary=True)
        if not data.startswith(b'\x89PNG\r\n\x1a\n'):
            raise ValueError('Server hat keine PNG-Aufnahme geliefert.')
        with output.open('xb') as file:
            file.write(data)
        return {'message': 'Bildschirmaufnahme gespeichert: ' + str(output)}

    def text(self, value):
        if self.session.get('text_input') is False:
            raise ValueError('Dieser TV-Dienst unterstützt noch keine Freitexteingabe. Bildschirmtastatur mit Pfeiltasten bedienen.')
        if not 1 <= len(value) <= 500:
            raise ValueError('Text muss 1–500 Zeichen lang sein.')
        return self.request('/api/input', {'text': value})


def decode_key(first, more):
    """Read ANSI arrows or Windows console extended keys without treating Esc as quit."""
    if first in ('\x00', '\xe0'):
        return {'H': 'up', 'P': 'down', 'K': 'left', 'M': 'right'}.get(more())
    if first == '\x1b':
        next_char = more()
        if next_char in ('[', 'O'):
            return {'A': 'up', 'B': 'down', 'C': 'right', 'D': 'left'}.get(more())
        return 'back' if next_char is None else None
    return {'w': 'up', 's': 'down', 'a': 'left', 'd': 'right', ' ': 'ok', '\r': 'ok',
            '\n': 'ok', '\x7f': 'back', '\x08': 'back', 'b': 'back', 'h': 'home',
            'm': 'menu', '+': 'volup', '-': 'voldown', '0': 'mute', 'p': 'play',
            'q': 'quit', '\x03': 'quit'}.get(first.lower())


def interactive(remote):
    if not sys.stdin.isatty():
        raise ValueError('Interaktiver Modus benötigt ein Terminal; alternativ „key ok“ verwenden.')
    print('Pfeile/WASD: bewegen · Enter/Leertaste: OK/Flappy · Esc/B: zurück\n'
          'H: Home · M: Menü · +/−: Lautstärke · 0: stumm · P: Play · Q/Ctrl+C: beenden\n'
          'Tasten wirken nur, solange dieses Terminal den Fokus hat. Keine Live-Spielansicht.')
    if sys.platform == 'win32':
        import msvcrt
        def read(timeout=None):
            deadline = None if timeout is None else time.monotonic() + timeout
            while not msvcrt.kbhit():
                if deadline is not None and time.monotonic() >= deadline:
                    return None
                time.sleep(.005)
            return msvcrt.getwch()
        cleanup = lambda: None
    else:
        import os
        import select
        import termios
        import tty
        fd = sys.stdin.fileno()
        previous = termios.tcgetattr(fd)
        tty.setcbreak(fd)
        def read(timeout=None):
            if not select.select([fd], [], [], timeout)[0]:
                return None
            value = os.read(fd, 1)
            return value.decode('ascii', 'ignore') if value else 'q'
        def cleanup():
            termios.tcsetattr(fd, termios.TCSADRAIN, previous)
    try:
        while True:
            key = decode_key(read(), lambda: read(.06))
            if key == 'quit':
                break
            if key:
                remote.key(key)
    finally:
        cleanup()


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--url', default='http://localhost:8765', help='Deck-Webserver, standardmäßig http://localhost:8765')
    parser.add_argument('--user', default='admin', help='Deck-Benutzername')
    parser.add_argument('--ca', help='Vertrauenswürdiges Server-Zertifikat (PEM) für den nativen HTTPS-Dienst')
    commands = parser.add_subparsers(dest='command')
    commands.add_parser('interactive', help='Pfeiltasten-Fernbedienung (Standard)')
    command = commands.add_parser('key', help='Einzelne Taste senden')
    command.add_argument('name', choices=list(KEYS))
    commands.add_parser('status', help='Gerätestatus lesen')
    commands.add_parser('wake', help='Über externen Deck-Server aus Standby wecken')
    command = commands.add_parser('capture', help='Bildschirmaufnahme als PNG speichern')
    command.add_argument('file', type=Path)
    command = commands.add_parser('text', help='Text in ein ausgewähltes TV-Eingabefeld senden')
    command.add_argument('value')
    args = parser.parse_args(argv)
    remote = None
    try:
        remote = Remote(args.url, args.ca)
        password = getpass.getpass(f'Deck-Passwort für {args.user}: ')
        remote.login(args.user, password)
        del password
        if args.command in (None, 'interactive'):
            interactive(remote)
            return 0
        if args.command == 'key':
            result = remote.key(args.name)
        elif args.command == 'wake':
            result = remote.wake()
        elif args.command == 'capture':
            result = remote.capture(args.file)
        elif args.command == 'text':
            result = remote.text(args.value)
        else:
            result = remote.request('/api/device')
        print(result['message'] if 'message' in result else json.dumps(result, ensure_ascii=False, indent=2))
        return 0
    except (OSError, ValueError, KeyError) as error:
        print('Fehler: ' + str(error), file=sys.stderr)
        return 1
    except (KeyboardInterrupt, EOFError):
        return 130
    finally:
        if remote:
            remote.logout()


if __name__ == '__main__':
    raise SystemExit(main())
