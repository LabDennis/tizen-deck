#!/usr/bin/env bash
set -euo pipefail
cd -- "$(dirname -- "$0")"
[[ $# == 1 ]] || { echo 'Usage: bash docker-install.sh TV_IPV4'; exit 1; }
python3 - "$1" <<'PY'
import ipaddress,sys
ipaddress.IPv4Address(sys.argv[1])
PY
umask 077
printf 'TIZEN_HOST=%s\n' "$1" > .env
if command -v docker >/dev/null; then
 docker compose up -d --build
 echo 'Open http://localhost:8765 on the Docker host. For another PC use an SSH tunnel to that host.'
 echo 'Initial credentials: docker compose exec deck cat /app/.state/initial-login.txt'
else
 echo 'Docker with Compose is required. No installation was started.' >&2
 exit 1
fi
