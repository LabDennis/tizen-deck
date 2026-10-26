#!/usr/bin/env bash
set -euo pipefail
cd -- "$(dirname -- "$0")"
if [[ $# != 1 ]]; then echo 'Usage: ./install-release-server.sh PC_LAN_IPV4' >&2; exit 2; fi
if [[ ! -f .state/release-server/config.json ]]; then
  python3 release_server.py init --bind "$1"
fi
python3 - "$PWD" "$1" <<'PY'
from pathlib import Path
import json,sys
root=Path(sys.argv[1]);config=json.loads((root/'.state/release-server/config.json').read_text())
if config['bind']!=sys.argv[2]:raise SystemExit('Existing feed uses another IP; preserve its TLS identity and reconfigure explicitly.')
# systemd quoted paths: no shell expansion; literal percent must be doubled.
def quote(value):return '"'+str(value).replace('\\','\\\\').replace('"','\\"').replace('%','%%')+'"'
unit=Path.home()/'.config/systemd/user/tizen-deck-updates.service'
text='[Unit]\nDescription=Private Tizen Deck LAN update feed\nAfter=network-online.target\nStartLimitIntervalSec=0\n[Service]\nType=simple\nUMask=0077\nExecStart=/usr/bin/python3 '+quote(root/'release_server.py')+' --state '+quote(root/'.state/release-server')+' serve\nRestart=on-failure\nRestartSec=15\nNoNewPrivileges=true\n[Install]\nWantedBy=default.target\n'
if unit.exists() and str(root/'release_server.py') not in unit.read_text():raise SystemExit('An unrelated service uses this name; not overwritten.')
unit.parent.mkdir(parents=True,exist_ok=True);unit.write_text(text)
PY
systemctl --user daemon-reload
systemctl --user enable --now tizen-deck-updates.service
systemctl --user is-active tizen-deck-updates.service
