# Native TV service and local updates

The Python backend can run externally in Docker; the .NET Core 3.1 backend runs
on the tested Samsung M7 itself. These are separate deployments and accounts.
The native service provides HTTPS on 8843 and a token-protected loopback
controller on 8844. Device tokens, certificates, signing keys and device WGTs
must stay outside Git.

## Build and install

1. Build the launcher with its own `build.py`.
2. Run `python tv-service/prepare_update.py /path/to/TizenDeckX.wgt` to create
   a local update signing key and signed package. Back up `.state/tv-service`
   privately; losing the key prevents further trusted updates.
3. Run `DOTNET_BIN=/path/to/dotnet bash tv-service/build.sh`, then
   `.venv/bin/python tv-service/install_tv.py` with the intended `TIZEN_HOST`.
4. Build the launcher with `DECK_DEVICE_CONFIG` pointing to the generated
   `.state/tv-service/launcher-config.js`; install this device-specific WGT.
5. In Settings → Netzwerk & Webserver → Webserver-Konfiguration set credentials and enable HTTPS.
   Passwords use salted PBKDF2 hashes. Trust the locally generated certificate
   on the client before login.

The installer verifies the transferred archive hash, preserves private state,
credentials, TLS identity, control token, privacy choices and update trust key.
It saves the previous service directory. Activation succeeds only after an
**authenticated loopback health check reports the expected backend version**;
otherwise the previous directory and service are restored. This is a startup
check, not proof that every feature remains healthy indefinitely.

The systemd unit restarts a failed backend at most three starts per minute.
**The native service still has no independent cold-boot startup.** Its unit is
in `/run`; reactivate it after a full reboot. This is separate from the working
Home-button and launcher autostart configuration described in DEFAULT-HOME.md.

## Launcher updates and rollback

First save a package that matches the currently installed launcher:

```sh
python tv-service/prepare_update.py /path/to/current/TizenDeckX.wgt --baseline
```

The baseline command refuses a version mismatch. Then stage a newer build:

```sh
python tv-service/prepare_update.py /path/to/new/TizenDeckX.wgt --stage
```

Settings → System → Tizen-Deck-Updates verifies and installs the package. Validation covers
the enrolled RSA signature, SHA-256, app/package IDs, XML version and size limits.
Normal installation refuses equal or older versions. The package manager performs
its own WGT checks, and the updater reads back the installed package version.

`updates/last-installed` holds the current signed package; `updates/rollback`
holds the preceding saved package. The menu offers explicit restoration, with
signature and version checks. If installation fails and a matching baseline is
available, the updater tries to reinstall it and verifies the result. If that
also fails, both errors are reported; it never claims recovery without readback.
Package-version verification is **not** a launcher render/health test.

On the M7, 1.7.0 → 1.6.0 → 1.7.0 rollback was tested on hardware. The current
1.8.3 launcher installation retains 1.8.2 as its immediate rollback.

Signed backend updates, automatic startup-failure rollback and a configurable
HTTPS feed are available in 1.8.2. See [BACKEND-UPDATES.md](BACKEND-UPDATES.md)
for packaging, private staging, device tests and limits. No live feed address is
configured yet. Samsung firmware updates are independent from this updater.

## Launcher crash handling

Launcher 1.7.2 sends a bounded heartbeat only while visible. Backend 1.7.2 also
counts PID changes when Tizen respawns the launcher between checks. A missing process
following a recent visible heartbeat can be restarted twice within two minutes;
a third detected loss opens Samsung Home and enters a two-minute cooldown.
Regular app transitions disarm the guard. Stale heartbeats and another foreground
app suppress recovery, so it must not interrupt Plex or Moonlight.

The authenticated loopback `GET /launcher/status` exposes the guard state and
last action for diagnosis. Unknown compositor focus suppresses a missing-process
restart; a new launcher PID confirming its own visibility can still be counted.

Hardware test: three deliberately terminated launcher processes were counted;
Samsung Home appeared after the third, verified by a compositor capture. Deck
was reopened afterward. A regular Plex launch disarmed the guard, and Home
returned to Deck with no recorded failure. This is a short recovery test, not a
long-term soak test.

This handles process disappearance, not all rendering hangs. It requires the
native backend to be running and does not rewrite the persistent Home mapping.

## Reversible privacy policy

Settings → Datenschutz-Dienste can mask and stop these five services:

- contents-recognition-service
- abnormal_user_behavior
- emergency-log-transmitter
- media-analytics-service
- kpi-logging

Before changes, the backend records which masks it owns and which services were
active. Undo removes its own masks and restarts only services that were active.
Masks already present from another administrator are not adopted. An enabled
policy is reapplied when the backend starts. The masks themselves live in `/run`:
without a running backend after cold boot they are not persistent.

`issue_report_agent` is retained pending investigation of its `devicetracker`
dependency. Firmware binaries are not deleted. This policy is not evidence of
complete tracking removal; traffic capture and broader dependency analysis remain
open. Enable and undo were both verified against the actual service states. The final
enabled policy was verified again after restarting the native backend.

## Hardware limits

- PNG captures, not encoded full-screen video; protected video/HDMI not guaranteed.
- Native compositor keys work; native free-text and mouse input remain open.
- Own volume/mute controls use Tizen TVAudioControl. Bluetooth pairing, audio
  device selection, Wi-Fi changes and brightness still use Samsung settings.
- No guaranteed wake alarm, full custom OS, or independent persistent root.

## Checks

```sh
python -m unittest discover -s tests -v
node tests/input.test.js
dotnet run --project tests/native-updates/Tests.csproj
dotnet run --project tests/native-apps/Tests.csproj
dotnet run --project tests/native-guard/Tests.csproj
```

The update test generates its own temporary signing key and fixtures. It tests
signature/hash failures, numeric versions, downgrade refusal, partial-install
recovery and reversible rollback without accessing a TV.
