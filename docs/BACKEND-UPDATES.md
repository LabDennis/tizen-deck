# Signed TV backend updates

Available from DeckTV and launcher 1.8.2 on the tested **LS32BM700UUXEN /
T-NKLBWWC-1720.7**. These packages update Deck's code, not Samsung firmware.
The native service must already be running. Cold-boot root startup remains open.

## Package and stage

Keep the existing enrolled RSA signing key in `.state/tv-service`. Never replace
it automatically during an update. Build and prepare a release:

```sh
DOTNET_BIN=/path/to/dotnet bash tv-service/build.sh
python tv-service/prepare_backend.py
python tv-service/manage_updates.py .state/backend-pending \
  --url https://MONITOR_IP:8843 --ca .state/tv-service/server.crt
```

The helper prompts for the current Deck password and uses the authenticated
HTTPS console. It does not perform a developer-mode/root bootstrap. For an
already running older Deck installation without the backend-update API, add
`--bootstrap-backend` once. A newer backend version is required.

The transfer verifies the enrolled public-key hash and file hashes. Packages
contain exactly eight code/static/unit files; private state is excluded. The
monitor verifies the RSA signature, SHA-256, firmware target, allowed ZIP paths,
file types, sizes and actual DLL version. Equal/older releases cannot be installed
as normal updates. A separately saved, hash-checked backup enables rollback.

Use **Einstellungen → System → Tizen-Deck-Updates → TV-Dienst aktualisieren** to install
or restore a previous version. The UI reconnects after the service restart;
a missing reply is shown as unknown state, not as proof that no update occurred.
The authenticated HTTPS API also exposes `/api/backend/updates`, `/install` and
`/rollback` under that prefix. Mutations require the normal session, origin and
CSRF checks, plus `confirm: true` for installation/rollback.

## Recovery behavior

A separate transient systemd job stops Deck, copies its latest private state and
launcher-update packages, saves the previous installation, swaps code and starts
it again. It commits only after systemd reports the service active and an
**authenticated HTTP health response reports the expected version**. On failure,
it restores the previous directory and performs the same check on that version.
Credentials, TLS/control identity, enrolled key and privacy settings are retained.

Hardware fault injection exposed a Samsung runtime quirk: an unsuccessful
`.NET --health-check` returned exit code 0. Recovery must therefore not depend
on that exit code. The worker now uses HTTP and version readback. The corrected
worker was tested on the monitor with a deliberately non-starting service; it
restored the previous working service automatically. The regular package was
then installed successfully. Explicit rollback 1.8.2 → 1.8.1 → 1.8.2
also passed with the expected version read back after each restart.

This protects against detected startup failure. Power loss or forcibly killing
the update worker during directory replacement is not covered by a proven
power-fail recovery mechanism. A backup remains on disk, but there is no claim of
a firmware recovery image or independent root startup after cold boot. Failed
updates keep their error state; inspect it before retrying. Backup folders are
private under `/opt/usr/tizen-deck-updates` and are currently retained.

## Private LAN release feed

DeckTV 1.9.1 and launcher 1.11.3 provide an authenticated HTTPS source hosted on the
owner's PC, for both launcher and backend packages. See [LAN-UPDATES.md](LAN-UPDATES.md)
for setup, certificate enrollment, publication and removal. Older direct-directory
backend-only URLs must be migrated to the new base URL with `backend/` and
`launcher/` subdirectories.

On the test monitor both component downloads and signature checks succeeded from
the PC source. Firmware and persistent boot behavior are unchanged. Launcher
packages containing device control configuration stay in the authenticated private
feed; do not publish them as public GitHub release assets.

## Tests

```sh
dotnet run --project tests/native-backend/Tests.csproj
python -m unittest discover -s tests -p 'test_backend_worker.py'
# In the launcher repository:
node tests/device-control.test.js
node tests/updates.browser.js
```

Tests cover altered signatures/hashes, incorrect component/firmware/version,
archive paths, symlinks and duplicates, allowed feed URLs, redirects/size limits,
state preservation, wrong-version recovery and restart-aware remote navigation.
