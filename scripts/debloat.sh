#!/bin/sh
# Historical M7 procedure; see docs/DEBLOAT.md before applying.
set -eu
if [ "${1:-}" != --apply ]; then
 echo 'Preview only. Read docs/DEBLOAT.md; --apply modifies this rooted TV.'
 exit 0
fi
[ "$(id -u)" = 0 ] || { echo 'Run on the rooted TV.' >&2; exit 1; }
[ -d /opt/usr/apps ] && [ -d /opt/usr/share/systemd/system ] || exit 1
# Keep all backups on-device; never commit or upload them.
umask 077
[ ! -e /opt/usr/share/codex-privacy-20261025/phase2 ] || { echo 'Backup already exists; stopping.' >&2; exit 1; }
vconftool get db/menu/support/softwareupdate/autoupdate
set -e; b=/opt/usr/share/codex-privacy-20261025/phase2; mkdir -p "$b"; pkgcmd -l > "$b/packages-before.txt"; systemctl list-units --type=service --all --no-pager > "$b/services-before.txt"; df -k /opt
set -e; tar -czf /opt/usr/share/codex-privacy-20261025/phase2/com.samsung.tizen.samsung-analytics.tar.gz -C /opt/usr/apps com.samsung.tizen.samsung-analytics; tar -tzf /opt/usr/share/codex-privacy-20261025/phase2/com.samsung.tizen.samsung-analytics.tar.gz >/dev/null; sha256sum /opt/usr/share/codex-privacy-20261025/phase2/com.samsung.tizen.samsung-analytics.tar.gz
set -e; tar -czf /opt/usr/share/codex-privacy-20261025/phase2/com.samsung.tv.context-aware-agent.tar.gz -C /opt/usr/apps com.samsung.tv.context-aware-agent; tar -tzf /opt/usr/share/codex-privacy-20261025/phase2/com.samsung.tv.context-aware-agent.tar.gz >/dev/null; sha256sum /opt/usr/share/codex-privacy-20261025/phase2/com.samsung.tv.context-aware-agent.tar.gz
set -e; tar -czf /opt/usr/share/codex-privacy-20261025/phase2/com.samsung.tv.context-aware-service.tar.gz -C /opt/usr/apps com.samsung.tv.context-aware-service; tar -tzf /opt/usr/share/codex-privacy-20261025/phase2/com.samsung.tv.context-aware-service.tar.gz >/dev/null; sha256sum /opt/usr/share/codex-privacy-20261025/phase2/com.samsung.tv.context-aware-service.tar.gz
set -e; tar -czf /opt/usr/share/codex-privacy-20261025/phase2/com.samsung.tv.tvplus.tar.gz -C /opt/usr/apps com.samsung.tv.tvplus; tar -tzf /opt/usr/share/codex-privacy-20261025/phase2/com.samsung.tv.tvplus.tar.gz >/dev/null; sha256sum /opt/usr/share/codex-privacy-20261025/phase2/com.samsung.tv.tvplus.tar.gz
set -e; tar -czf /opt/usr/share/codex-privacy-20261025/phase2/com.samsung.tv.tvPlusDeeplinker.tar.gz -C /opt/usr/apps com.samsung.tv.tvPlusDeeplinker; tar -tzf /opt/usr/share/codex-privacy-20261025/phase2/com.samsung.tv.tvPlusDeeplinker.tar.gz >/dev/null; sha256sum /opt/usr/share/codex-privacy-20261025/phase2/com.samsung.tv.tvPlusDeeplinker.tar.gz
set -e; tar -czf /opt/usr/share/codex-privacy-20261025/phase2/com.samsung.tv.wakeup-engine-bixby.tar.gz -C /opt/usr/apps com.samsung.tv.wakeup-engine-bixby; tar -tzf /opt/usr/share/codex-privacy-20261025/phase2/com.samsung.tv.wakeup-engine-bixby.tar.gz >/dev/null; sha256sum /opt/usr/share/codex-privacy-20261025/phase2/com.samsung.tv.wakeup-engine-bixby.tar.gz
set -e; tar -czf /opt/usr/share/codex-privacy-20261025/phase2/com.samsung.tv.wakeup-engine-alexa.tar.gz -C /opt/usr/apps com.samsung.tv.wakeup-engine-alexa; tar -tzf /opt/usr/share/codex-privacy-20261025/phase2/com.samsung.tv.wakeup-engine-alexa.tar.gz >/dev/null; sha256sum /opt/usr/share/codex-privacy-20261025/phase2/com.samsung.tv.wakeup-engine-alexa.tar.gz
set -e; tar -czf /opt/usr/share/codex-privacy-20261025/phase2/com.samsung.tv.alexa-client.tar.gz -C /opt/usr/apps com.samsung.tv.alexa-client; tar -tzf /opt/usr/share/codex-privacy-20261025/phase2/com.samsung.tv.alexa-client.tar.gz >/dev/null; sha256sum /opt/usr/share/codex-privacy-20261025/phase2/com.samsung.tv.alexa-client.tar.gz
set -e; tar -czf /opt/usr/share/codex-privacy-20261025/phase2/com.samsung.tv.bixby-prov.tar.gz -C /opt/usr/apps com.samsung.tv.bixby-prov; tar -tzf /opt/usr/share/codex-privacy-20261025/phase2/com.samsung.tv.bixby-prov.tar.gz >/dev/null; sha256sum /opt/usr/share/codex-privacy-20261025/phase2/com.samsung.tv.bixby-prov.tar.gz
set -e; tar -czf /opt/usr/share/codex-privacy-20261025/phase2/com.samsung.tv.mybixby.tar.gz -C /opt/usr/apps com.samsung.tv.mybixby; tar -tzf /opt/usr/share/codex-privacy-20261025/phase2/com.samsung.tv.mybixby.tar.gz >/dev/null; sha256sum /opt/usr/share/codex-privacy-20261025/phase2/com.samsung.tv.mybixby.tar.gz
set -e; tar -czf /opt/usr/share/codex-privacy-20261025/phase2/com.samsung.tv.bixbycapsuleviewer.tar.gz -C /opt/usr/apps com.samsung.tv.bixbycapsuleviewer; tar -tzf /opt/usr/share/codex-privacy-20261025/phase2/com.samsung.tv.bixbycapsuleviewer.tar.gz >/dev/null; sha256sum /opt/usr/share/codex-privacy-20261025/phase2/com.samsung.tv.bixbycapsuleviewer.tar.gz
set -e; tar -czf /opt/usr/share/codex-privacy-20261025/phase2/com.samsung.tv.ondevice-voice.tar.gz -C /opt/usr/apps com.samsung.tv.ondevice-voice; tar -tzf /opt/usr/share/codex-privacy-20261025/phase2/com.samsung.tv.ondevice-voice.tar.gz >/dev/null; sha256sum /opt/usr/share/codex-privacy-20261025/phase2/com.samsung.tv.ondevice-voice.tar.gz
set -e; tar -czf /opt/usr/share/codex-privacy-20261025/phase2/com.samsung.tv.farfieldvoice.tar.gz -C /opt/usr/apps com.samsung.tv.farfieldvoice; tar -tzf /opt/usr/share/codex-privacy-20261025/phase2/com.samsung.tv.farfieldvoice.tar.gz >/dev/null; sha256sum /opt/usr/share/codex-privacy-20261025/phase2/com.samsung.tv.farfieldvoice.tar.gz
set -e; tar -czf /opt/usr/share/codex-privacy-20261025/phase2/com.samsung.tizen.smartthings-client.tar.gz -C /opt/usr/apps com.samsung.tizen.smartthings-client; tar -tzf /opt/usr/share/codex-privacy-20261025/phase2/com.samsung.tizen.smartthings-client.tar.gz >/dev/null; sha256sum /opt/usr/share/codex-privacy-20261025/phase2/com.samsung.tizen.smartthings-client.tar.gz
set -e; tar -czf /opt/usr/share/codex-privacy-20261025/phase2/com.samsung.tv.smartthingsfind-daemon.tar.gz -C /opt/usr/apps com.samsung.tv.smartthingsfind-daemon; tar -tzf /opt/usr/share/codex-privacy-20261025/phase2/com.samsung.tv.smartthingsfind-daemon.tar.gz >/dev/null; sha256sum /opt/usr/share/codex-privacy-20261025/phase2/com.samsung.tv.smartthingsfind-daemon.tar.gz
set -e; tar -czf /opt/usr/share/codex-privacy-20261025/phase2/com.samsung.tv.iot-hub-control.tar.gz -C /opt/usr/apps com.samsung.tv.iot-hub-control; tar -tzf /opt/usr/share/codex-privacy-20261025/phase2/com.samsung.tv.iot-hub-control.tar.gz >/dev/null; sha256sum /opt/usr/share/codex-privacy-20261025/phase2/com.samsung.tv.iot-hub-control.tar.gz
set -e; tar -czf /opt/usr/share/codex-privacy-20261025/phase2/samsunghealth-service-tv.tar.gz -C /opt/usr/apps samsunghealth-service-tv; tar -tzf /opt/usr/share/codex-privacy-20261025/phase2/samsunghealth-service-tv.tar.gz >/dev/null; sha256sum /opt/usr/share/codex-privacy-20261025/phase2/samsunghealth-service-tv.tar.gz
set -e; vconftool get db/menu/support/softwareupdate/autoupdate > /opt/usr/share/codex-privacy-20261025/autoupdate-before.txt; vconftool set -t int db/menu/support/softwareupdate/autoupdate 0; vconftool get db/menu/support/softwareupdate/autoupdate
pkgcmd -u -n com.samsung.tizen.samsung-analytics
pkgcmd -u -n com.samsung.tv.context-aware-agent
pkgcmd -u -n com.samsung.tv.context-aware-service
pkgcmd -u -n com.samsung.tv.tvplus
pkgcmd -u -n com.samsung.tv.tvPlusDeeplinker
pkgcmd -u -n com.samsung.tv.wakeup-engine-bixby
pkgcmd -u -n com.samsung.tv.wakeup-engine-alexa
pkgcmd -u -n com.samsung.tv.alexa-client
pkgcmd -u -n com.samsung.tv.bixby-prov
pkgcmd -u -n com.samsung.tv.mybixby
pkgcmd -u -n com.samsung.tv.bixbycapsuleviewer
pkgcmd -u -n com.samsung.tv.ondevice-voice
pkgcmd -u -n com.samsung.tv.farfieldvoice
pkgcmd -u -n com.samsung.tizen.smartthings-client
pkgcmd -u -n com.samsung.tv.smartthingsfind-daemon
pkgcmd -u -n com.samsung.tv.iot-hub-control
pkgcmd -u -n samsunghealth-service-tv
pkgcmd -l -t tpk; pkgcmd -l -t wgt; systemctl show connman.service wpa_supplicant.service bluetooth-frwk-service.service btapp-service.service -p Id -p ActiveState; systemctl --failed --no-pager; vconftool get db/menu/support/softwareupdate/autoupdate
set -e; for p in com.samsung.tv.ondevice-voice com.samsung.tv.context-aware-agent com.samsung.tv.context-aware-service; do test -e /opt/usr/share/systemd/system/$p || : > /opt/usr/share/systemd/system/$p; done; systemctl stop --no-block com.samsung.tv.context-aware-agent.service com.samsung.tv.context-aware-service.service; systemctl reset-failed com.samsung.tv.ondevice-voice.service; systemctl show com.samsung.tv.context-aware-agent.service com.samsung.tv.context-aware-service.service connman.service bluetooth-frwk-service.service btapp-service.service -p Id -p MainPID -p ActiveState; vconftool get db/menu/support/softwareupdate/autoupdate
