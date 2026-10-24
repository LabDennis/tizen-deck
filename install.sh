#!/usr/bin/env bash
set -euo pipefail
cd -- "$(dirname -- "$0")"
if [[ ${1:-} == --help || $# -lt 1 ]]; then
  echo 'Usage: bash install.sh TV_IPV4 [--root-support]'; exit 0
fi
TV_HOST=$1; shift
ROOT_SUPPORT=false
if [[ ${1:-} == --root-support ]]; then ROOT_SUPPORT=true; shift; fi
[[ $# == 0 ]] || { echo 'Unknown argument' >&2; exit 1; }
[[ $(uname -s) == Linux && $(uname -m) == x86_64 ]] || { echo 'Installer currently supports Linux x86_64 only.' >&2; exit 1; }
python3 - "$TV_HOST" <<'PY'
import ipaddress,sys
assert sys.version_info >= (3,12), 'Python 3.12+ required'
ipaddress.IPv4Address(sys.argv[1])
PY
command -v git >/dev/null
command -v curl >/dev/null
umask 077
mkdir -p .deps .state
python3 -m venv .venv
.venv/bin/python -m pip install -r requirements.txt
if [[ ! -d .deps/samsung-tv-root/.git ]]; then git clone https://github.com/chris-ritsen/samsung-tv-root.git .deps/samsung-tv-root; fi
git -C .deps/samsung-tv-root checkout --detach 3b97edf3762db7670585d73e29623dc56195bf98
curl -fL --retry 2 https://download.tizen.org/sdk/tizenstudio/official/binary/sdb_4.2.36_ubuntu-64.zip -o .deps/sdb.zip
printf '%s\n' 'addf825e1a886a9f220cb3d287e74438cae0ffafe5821b9baca9d4f6c7de5124  .deps/sdb.zip' | sha256sum -c -
.venv/bin/python -m zipfile -e .deps/sdb.zip .deps/tizen-sdb
chmod +x .deps/tizen-sdb/data/tools/sdb
if $ROOT_SUPPORT; then
  curl -fL --retry 2 https://github.com/chris-ritsen/samsung-tv-root/releases/download/v0.0.12/samsung-tv-root-0.0.12-linux-x86_64.tar.gz -o .deps/root.tar.gz
  printf '%s\n' 'f7aa3a3b550a65bc7d29b1b6b3fdcd3876fdcaa41dec4766f00ec7e3e64a3232  .deps/root.tar.gz' | sha256sum -c -
  mkdir -p .deps/root-release
  .venv/bin/python - <<'PY'
import tarfile
with tarfile.open('.deps/root.tar.gz') as archive:
    archive.extractall('.deps/root-release',filter='data')
PY
fi
.venv/bin/python - "$TV_HOST" <<'PY'
import json,sys
from pathlib import Path
p=Path('.state/config.json');p.write_text(json.dumps({'tv_host':sys.argv[1]}));p.chmod(0o600)
PY
echo 'Installed locally. Start: bash start.sh  |  http://localhost:8765'
echo 'No TV changes, firewall changes or boot persistence were applied.'
