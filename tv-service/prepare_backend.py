#!/usr/bin/env python3
"""Build a signed DeckTV backend package. Contains code only; no device state."""
import argparse
import hashlib
import json
import re
import subprocess
import zipfile
from pathlib import Path
import xml.etree.ElementTree as ET

HERE = Path(__file__).resolve().parent
FILES = ('DeckTV.dll', 'DeckTV.deps.json', 'DeckTV.runtimeconfig.json', 'deck-tv.service',
         'static/index.html', 'static/app.js', 'static/input.js', 'static/style.css')


def prepare(dist, state, out, version=None):
    version = version or ET.parse(HERE / 'DeckTV.csproj').findtext('.//Version')
    if not re.fullmatch(r'[0-9]{1,4}\.[0-9]{1,4}\.[0-9]{1,4}', version or ''):
        raise ValueError('Invalid version')
    key = state / 'update-private.pem'
    if not key.is_file():
        raise ValueError('Use the existing enrolled update signing key; none was found')
    out.mkdir(parents=True, exist_ok=True, mode=0o700)
    package = out / 'backend.zip'
    with zipfile.ZipFile(package, 'w', zipfile.ZIP_DEFLATED) as archive:
        for name in FILES:
            path = dist / name
            if name == 'deck-tv.service' and not path.exists():
                path = HERE / name
            if path.is_symlink() or not path.is_file() or path.stat().st_size > 8388608:
                raise ValueError('Missing, linked or oversized package file: ' + name)
            archive.write(path, name)
    manifest = {'component': 'DeckTV', 'version': version,
                'sha256': hashlib.sha256(package.read_bytes()).hexdigest(),
                'firmware': 'T-NKLBWWC-1720.7'}
    (out / 'manifest.json').write_text(json.dumps(manifest, sort_keys=True, separators=(',', ':')))
    subprocess.run(['openssl', 'dgst', '-sha256', '-sign', str(key), '-out',
                    str(out / 'manifest.sig'), str(out / 'manifest.json')], check=True)
    return manifest


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--dist', type=Path, default=HERE / 'dist')
    parser.add_argument('--state', type=Path, default=HERE.parent / '.state/tv-service')
    parser.add_argument('--out', type=Path, default=HERE.parent / '.state/backend-pending')
    args = parser.parse_args()
    print(json.dumps(prepare(args.dist, args.state, args.out), indent=2))
