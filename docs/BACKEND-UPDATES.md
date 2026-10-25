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

Use **Einstellungen → Tizen-Deck-Updates → TV-Dienst aktualisieren** to install
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

## Optional release feed

**Release-Adresse konfigurieren** accepts an HTTPS directory containing exactly:

- `manifest.json`
- `manifest.sig` (binary RSA/SHA-256 signature)
- `backend.zip`

The directory is user-configured; no vendor endpoint or automatic polling is
built in. It must use a TLS certificate trusted by the monitor, with no HTTP
redirect, URL credentials, query string or fragment. Downloads are bounded to
25 seconds overall and 16 MiB for the archive. Downloading and verifying a
release only stages it; installation is a separate action. A failed verification
keeps the previously staged package.

The feed supports backend code only. **Do not publish a device-specific launcher
WGT:** it contains that monitor's control configuration. Stage launcher updates
privately using the same HTTPS helper instead:

```sh
python tv-service/prepare_update.py /path/to/TizenDeckX.wgt
python tv-service/manage_updates.py .state/tv-service/pending-update \
  --url https://MONITOR_IP:8843 --ca .state/tv-service/server.crt
```

No live release-feed address is configured for the test monitor yet. URL checks,
bounded downloads, rejection cases and signature/package validation are tested
locally; an actual hosted HTTPS feed still requires its address and a live test.

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
