#!/usr/bin/env python3
"""Private, read-only HTTPS feed for this installation's signed Deck packages."""
import argparse
import hashlib
import hmac
import http.server
import ipaddress
import json
import os
from pathlib import Path
import secrets
import shutil
import ssl
import subprocess
import sys
import tempfile

FILES = {'backend': ('manifest.json', 'manifest.sig', 'backend.zip'),
         'launcher': ('manifest.json', 'manifest.sig', 'launcher.wgt')}
DEFAULT = Path(__file__).resolve().parent / '.state/release-server'


def atomic_json(path, value):
    with tempfile.NamedTemporaryFile(mode='w', dir=path.parent, delete=False) as f:
        json.dump(value, f)
        name = f.name
    os.replace(name, path)


def init(state, bind, port):
    ip = ipaddress.ip_address(bind)
    if not ip.is_private or ip.is_unspecified or ip.is_loopback:
        raise ValueError('Use a specific LAN IP reachable by the monitor')
    if not 1024 <= port <= 65535:
        raise ValueError('Port must be 1024–65535')
    state.mkdir(parents=True, exist_ok=True, mode=0o700)
    if (state / 'config.json').exists():
        raise ValueError('Already configured; existing credentials were preserved')
    # Never overwrite a previous key left by an interrupted setup.
    if any((state / name).exists() for name in ('server.key', 'server.crt')):
        raise ValueError('Existing TLS files; inspect interrupted setup first')
    subprocess.run(['openssl', 'req', '-x509', '-newkey', 'rsa:3072', '-nodes',
                    '-days', '825', '-subj', '/CN=Tizen Deck LAN Updates',
                    '-addext', f'subjectAltName=IP:{bind}', '-keyout', str(state / 'server.key'),
                    '-out', str(state / 'server.crt')], check=True, capture_output=True)
    (state / 'server.key').chmod(0o600)
    der = ssl.PEM_cert_to_DER_cert((state / 'server.crt').read_text())
    config = {'bind': bind, 'port': port, 'access_token': secrets.token_urlsafe(32),
              'certificate_sha256': hashlib.sha256(der).hexdigest()}
    atomic_json(state / 'config.json', config)
    atomic_json(state / 'current.json', {})
    print(f'Private feed prepared: https://{bind}:{port}/')


def publish(state, package, public_key):
    sys.path.insert(0, str(Path(__file__).resolve().parent / 'tv-service'))
    from manage_updates import validate_package
    manifest, artifact = validate_package(package, public_key)
    component = 'backend' if artifact == 'backend.zip' else 'launcher'
    relative = Path('packages') / component / (manifest['version'] + '-' + manifest['sha256'])
    destination = state / relative
    if destination.exists():
        validate_package(destination, public_key)
    else:
        destination.parent.mkdir(parents=True, exist_ok=True, mode=0o700)
        with tempfile.TemporaryDirectory(dir=destination.parent) as td:
            staging = Path(td) / 'package'
            staging.mkdir(mode=0o700)
            for name in FILES[component]:
                shutil.copyfile(package / name, staging / name)
                (staging / name).chmod(0o600)
            validate_package(staging, public_key)
            staging.rename(destination)
    current = json.loads((state / 'current.json').read_text())
    current[component] = relative.as_posix()
    atomic_json(state / 'current.json', current)
    print(f'Published {component} {manifest["version"]} to private LAN feed')


class Feed(http.server.BaseHTTPRequestHandler):
    def log_message(self, *_):
        pass  # No credentials, URLs or client identifiers in routine logs.

    def do_GET(self):
        token = self.headers.get('Authorization', '')
        if not hmac.compare_digest(token, 'Bearer ' + self.server.token):
            self.send_error(401, 'Authentication required')
            return
        route = self.path.split('/')
        if len(route) != 3 or route[1] not in FILES or route[2] not in FILES[route[1]]:
            self.send_error(404)
            return
        try:
            root = self.server.state.resolve()
            current = json.loads((root / 'current.json').read_text())
            file = (root / current[route[1]] / route[2]).resolve()
            if not file.is_relative_to(root / 'packages') or file.stat().st_size > 16777216:
                raise ValueError('Invalid package location')
            data = file.read_bytes()
        except (OSError, KeyError, ValueError):
            self.send_error(404)
            return
        self.send_response(200)
        self.send_header('Content-Type', 'application/octet-stream')
        self.send_header('Content-Length', str(len(data)))
        self.send_header('Cache-Control', 'no-store')
        self.send_header('X-Content-Type-Options', 'nosniff')
        self.end_headers()
        self.wfile.write(data)


class Server(http.server.HTTPServer):
    # Single reader: bounded memory and no unbounded request-thread creation.
    def get_request(self):
        connection, address = super().get_request()
        connection.settimeout(8)
        try:
            return self.tls.wrap_socket(connection, server_side=True), address
        except Exception:
            connection.close()
            raise


def serve(state):
    c = json.loads((state / 'config.json').read_text())
    tls = ssl.SSLContext(ssl.PROTOCOL_TLS_SERVER)
    tls.minimum_version = ssl.TLSVersion.TLSv1_2
    tls.load_cert_chain(state / 'server.crt', state / 'server.key')
    with Server((c['bind'], c['port']), Feed) as server:
        server.tls, server.state, server.token = tls, state, c['access_token']
        print(f'Deck release feed listening on {c["bind"]}:{c["port"]}', flush=True)
        server.serve_forever()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--state', type=Path, default=DEFAULT)
    sub = parser.add_subparsers(dest='command', required=True)
    setup = sub.add_parser('init')
    setup.add_argument('--bind', required=True)
    setup.add_argument('--port', type=int, default=8845)
    sub.add_parser('serve')
    pub = sub.add_parser('publish')
    pub.add_argument('package', type=Path)
    pub.add_argument('--public-key', type=Path, default=DEFAULT.parent / 'tv-service/update-public.der')
    args = parser.parse_args()
    os.umask(0o077)
    if args.command == 'init': init(args.state, args.bind, args.port)
    elif args.command == 'publish': publish(args.state, args.package, args.public_key)
    else: serve(args.state)


if __name__ == '__main__':
    main()
