# Native TV service and local launcher updates

Two deployments are available: the Python backend in Docker on a Linux host,
and `tv-service` as a .NET Core 3.1 service directly on the tested Samsung monitor.
They are separate deployments with separate credentials. The native service uses
HTTPS 8843 for the browser and a token-protected loopback controller on 8844 for
the launcher. The control token and TLS/private signing keys must never enter Git.

## Build and install

1. Build the launcher with its own `build.py`.
2. Run `python tv-service/prepare_update.py /path/to/TizenDeckX.wgt` to create
   the local update signing key and a signed package. Keep `.state/tv-service`
   private and backed up: losing the signing key prevents further trusted updates.
3. Run `DOTNET_BIN=/path/to/dotnet bash tv-service/build.sh`, then
   `.venv/bin/python tv-service/install_tv.py` with the intended `TIZEN_HOST`.
4. Build the launcher again with `DECK_DEVICE_CONFIG` pointing to the generated
   `.state/tv-service/launcher-config.js`; install this device-specific WGT using
   the launcher installer. Do not distribute that WGT to another device.
5. In Settings → Webserver-Konfiguration set credentials and enable the listener.
   The credentials are stored as a salted PBKDF2 hash; a browser must trust the
   device's locally generated certificate before login.

The installer preserves credentials, the enabled flag, and the originally enrolled
update verification key. It keeps the previous service directory and restores it
if the new service fails its immediate systemd activation check. This is not a
complete application health check. Inspect logs if it fails later.

**Cold-boot autostart is not installed.** The systemd unit lives in `/run` and
needs reactivation after a full reboot. No working autostart toggle is claimed.
The Samsung launcher and firmware partitions remain intact.

## Updating our launcher

`python tv-service/prepare_update.py /path/to/new/TizenDeckX.wgt --stage`

This signs the manifest and stages a local package over the existing authorized
SDB/root bridge. It does not launch an app or install the staged package. The TV
menu **Settings → Tizen-Deck-Updates** verifies and installs it explicitly.
The verifier checks an enrolled RSA signature, SHA-256, package/application IDs,
XML version, package size, and metadata size. The Tizen package manager separately
checks the WGT. The currently running launcher may close during replacement;
reopen it afterward. No automatic online download or GitHub-release feed exists.
The last successful package is retained as `updates/last-installed`; automatic
launcher rollback is not implemented. Downgrades signed by the enrolled key are
allowed deliberately for manual recovery.

The native backend is upgraded with `install_tv.py`, not through the launcher
update button. Samsung's firmware update setting is a separate API and is not
changed by this updater.

## Current hardware limitations

- Real compositor PNG snapshots, not encoded video streaming; protected video
  or HDMI visibility is not guaranteed.
- Native uinput mouse/keys are experimental. Native free-text input is not yet
  available; the external Samsung remote bridge supports text where the app does.
- Bluetooth audio opens the tested native audio picker. Keyboard/mouse/controller
  currently opens general Samsung settings rather than a dedicated HID picker.
- No guaranteed wake alarm, default-Home replacement, or boot persistence yet.

## Checks

`python -m unittest discover -s tests -v` tests the external server without a TV.
`node tests/input.test.js` checks hover-scoped input.
For the native signature verifier, after preparing a package:

```
dotnet run --project tests/native-updates/Tests.csproj -- \
  "$PWD/.state/tv-service/pending-update" \
  "$PWD/.state/tv-service/update-public.der"
```

This test accepts the valid package, rejects modified manifest/signature/payload,
and checks an empty staging directory. It does not install anything on a TV.
