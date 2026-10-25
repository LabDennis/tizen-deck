#!/usr/bin/env bash
set -euo pipefail
cd -- "$(dirname -- "$0")"
DOTNET_CLI_TELEMETRY_OPTOUT=1 "${DOTNET_BIN:-dotnet}" publish DeckTV.csproj -c Release -o dist
mkdir -p dist/static
cp ../static/* dist/static/
echo 'Native TV files in tv-service/dist; installer adds private device configuration.'
