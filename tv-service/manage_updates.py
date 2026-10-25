#!/usr/bin/env python3
"""Stage signed updates through an already running, authenticated native Deck server.

No developer-mode bootstrap is performed. --bootstrap-backend upgrades an older
Deck service once so that subsequent updates can use the TV menu.
"""
import argparse
import base64
import getpass
import hashlib
import io
import json
import re
import shlex
import subprocess
import sys
import tarfile
import tempfile
import time
import uuid
import zipfile
from pathlib import Path
HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE.parent))
from pc_remote import Remote
from prepare_backend import FILES

ROOT = '/opt/usr/tizen-deck'
DEPOT = '/opt/usr/tizen-deck-updates'


def command(remote, value):
    result = remote.job('/api/console', {'mode': 'root', 'confirm_root': True, 'command': value})
    if result['exit_code']:
        raise ValueError(result['output'][-2000:])
    return result['output']


def upload(remote, data, path):
    # Generated path only, per-chunk hashes checked by a final whole-file hash.
    quoted = shlex.quote(path)
    command(remote, 'umask 077; : > ' + quoted)
    for start in range(0, len(data), 2100):
        encoded = base64.b64encode(data[start:start + 2100]).decode('ascii')
        command(remote, "printf '%s' '" + encoded + "' | base64 -d >> " + quoted)
    command(remote, 'test "$(sha256sum ' + quoted + " | cut -d ' ' -f1)\" = " + shlex.quote(hashlib.sha256(data).hexdigest()))


def validate_package(directory, public):
    data = (directory / 'manifest.json').read_bytes()
    if len(data) > 4096 or (directory / 'manifest.sig').stat().st_size > 1024:
        raise ValueError('Metadata too large')
    with tempfile.TemporaryDirectory() as td:
        pem = Path(td) / 'public.pem'
        subprocess.run(['openssl', 'pkey', '-pubin', '-inform', 'DER', '-in', str(public), '-out', str(pem)], check=True, capture_output=True)
        subprocess.run(['openssl', 'dgst', '-sha256', '-verify', str(pem), '-signature', str(directory / 'manifest.sig'), str(directory / 'manifest.json')], check=True, capture_output=True)
    manifest = json.loads(data)
    if not re.fullmatch(r'[0-9]{1,4}\.[0-9]{1,4}\.[0-9]{1,4}', manifest.get('version', '')):
        raise ValueError('Invalid version')
    backend = manifest.get('component') == 'DeckTV'
    if not backend and manifest.get('component') != 'TizenDeckX.XMB':
        raise ValueError('Unknown component')
    if backend and manifest.get('firmware') != 'T-NKLBWWC-1720.7':
        raise ValueError('Unsupported firmware')
    name = 'backend.zip' if backend else 'launcher.wgt'
    if (directory / name).stat().st_size > 16777216 or hashlib.sha256((directory / name).read_bytes()).hexdigest() != manifest['sha256']:
        raise ValueError('Package hash/size mismatch')
    if backend:
        with zipfile.ZipFile(directory / name) as archive:
            entries = archive.infolist()
            if len(entries) != len(FILES) or {x.filename for x in entries} != set(FILES):
                raise ValueError('Invalid backend file list')
            if sum(x.file_size for x in entries) > 33554432 or any(x.file_size > 8388608 or ((x.external_attr >> 16) & 0xF000) not in (0, 0x8000) for x in entries):
                raise ValueError('Unsafe archive type or size')
    return manifest, name


def stage(remote, directory, public, bootstrap=False):
    manifest, name = validate_package(directory, public)
    expected_key = hashlib.sha256(public.read_bytes()).hexdigest()
    command(remote, 'test "$(sha256sum '+ROOT+'/state/update-public.der | cut -d " " -f1)" = '+shlex.quote(expected_key))
    tag = uuid.uuid4().hex
    incoming = DEPOT + '/incoming-' + tag
    command(remote, 'umask 077; mkdir -p '+DEPOT+'; chmod 700 '+DEPOT+'; mkdir '+incoming)
    buffer = io.BytesIO()
    with tarfile.open(fileobj=buffer, mode='w:gz') as archive:
        for file in ('manifest.json', 'manifest.sig', name):
            archive.add(directory / file, arcname=file, recursive=False)
    upload(remote, buffer.getvalue(), incoming+'/package.tar.gz')
    command(remote, 'set -e; tar -xzf '+incoming+'/package.tar.gz -C '+incoming+'; rm '+incoming+'/package.tar.gz')
    slot = DEPOT+'/pending' if name == 'backend.zip' else ROOT+'/updates/pending'
    previous = slot+'.stage-'+tag
    command(remote, 'set -e; mkdir -p '+shlex.quote(str(Path(slot).parent))+'; if [ -d '+slot+' ]; then mv '+slot+' '+previous+'; fi; if mv '+incoming+' '+slot+'; then rm -rf '+previous+'; else [ ! -d '+previous+' ] || mv '+previous+' '+slot+'; exit 1; fi')
    if bootstrap:
        if name != 'backend.zip': raise ValueError('Bootstrap only accepts a backend')
        bootstrap_backend(remote, directory, manifest, tag)
    return manifest


def bootstrap_backend(remote, directory, manifest, tag):
    # The client verifies the enrolled signature before extraction; server verifies
    # transfer hashes. New versions verify packages inside BackendUpdates instead.
    health = json.loads(command(remote, 'curl --fail --silent --show-error --max-time 5 -H "X-Deck-Control: $(cat '+ROOT+'/state/control-token)" http://127.0.0.1:8844/health'))
    if tuple(map(int, manifest['version'].split('.'))) <= tuple(map(int, health['version'].split('.'))):
        raise ValueError('Bootstrap requires a newer version')
    command(remote, "grep -q 'T-NKLBWWC-1720.7' /etc/prd_info.ini")
    next_dir, backup, job = (DEPOT+'/'+x+'-'+tag for x in ('next', 'backup', 'job'))
    command(remote, 'umask 077; mkdir '+next_dir+' '+job)
    buffer = io.BytesIO()
    with zipfile.ZipFile(directory/'backend.zip') as zip_file, tarfile.open(fileobj=buffer, mode='w:gz') as archive:
        for name in FILES:
            content = zip_file.read(name)
            info = tarfile.TarInfo(name); info.size=len(content); info.mode=0o600
            archive.addfile(info, io.BytesIO(content))
    upload(remote, buffer.getvalue(), job+'/code.tar.gz')
    command(remote, 'set -e; tar -xzf '+job+'/code.tar.gz -C '+next_dir+'; rm '+job+'/code.tar.gz')
    hashes = {}
    for name in FILES:
        hashes[name] = command(remote, 'sha256sum '+ROOT+'/'+name).split()[0]
    upload(remote, json.dumps({'path':backup,'version':health['version'],'hashes':hashes}).encode(), job+'/previous.json')
    worker = (HERE/'backend-update.sh').read_text().replace('NEXT_PATH',next_dir).replace('BACKUP_PATH',backup).replace('JOB_PATH',job).replace('NEW_VERSION',manifest['version']).replace('OLD_VERSION',health['version'])
    upload(remote, worker.encode(), job+'/worker.sh')
    upload(remote, json.dumps({'phase':'running','version':manifest['version'],'message':'Backend-Wechsel vorbereitet'}).encode(), DEPOT+'/result.json')
    command(remote, 'systemd-run --unit=deck-update-'+tag+' --on-active=2s /bin/sh -c '+shlex.quote('cat '+job+'/worker.sh | /bin/sh'))


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('directory', type=Path)
    parser.add_argument('--url', required=True)
    parser.add_argument('--ca', required=True)
    parser.add_argument('--public-key', type=Path, default=HERE.parent/'.state/tv-service/update-public.der')
    parser.add_argument('--user',default='admin')
    parser.add_argument('--bootstrap-backend',action='store_true')
    args=parser.parse_args()
    remote=Remote(args.url,args.ca)
    try:
        remote.login(args.user,getpass.getpass('Deck password: '))
        print(json.dumps(stage(remote,args.directory,args.public_key,args.bootstrap_backend),indent=2))
        print('Service switch scheduled; reconnect and check status.' if args.bootstrap_backend else 'Staged. Install from Settings → Tizen-Deck-Updates.')
    finally: remote.logout()

if __name__=='__main__':main()
