#!/bin/sh
# Historical M7 procedure; see docs/DEBLOAT.md before applying.
set -eu
if [ "${1:-}" != --apply ]; then
 echo 'Preview only. Read docs/DEBLOAT.md; --apply modifies this rooted TV.'
 exit 0
fi
[ "$(id -u)" = 0 ] || { echo 'Run on the rooted TV.' >&2; exit 1; }
[ -d /opt/usr/apps ] && [ -d /opt/usr/share/systemd/system ] || exit 1
set -eu
base=/opt/usr/share/systemd/system
backup=/opt/usr/share/codex-privacy-20261025
[ ! -e "$backup" ] || { echo 'Backup path already exists; stopping'; exit 2; }
for m in pisa-service canalysis-daemon; do
 [ ! -e "$base/$m" ] && [ ! -L "$base/$m" ] || { echo "Existing marker $m; stopping"; exit 2; }
done
for u in media-analytics-service.service kpi-logging.service; do
 [ ! -e "/run/systemd/system/$u" ] && [ ! -L "/run/systemd/system/$u" ] || { echo "Existing override $u; stopping"; exit 2; }
done
mkdir -m 700 "$backup"
cat > "$backup/rollback.sh" <<'ROLLBACK'
#!/bin/sh
set -eu
rm -f /opt/usr/share/systemd/system/pisa-service /opt/usr/share/systemd/system/canalysis-daemon
for u in media-analytics-service.service kpi-logging.service; do
 p="/run/systemd/system/$u"
 if [ "$(readlink "$p" 2>/dev/null || true)" = /dev/null ]; then rm "$p"; fi
done
systemctl daemon-reload
systemctl start adagent-service.service pisa-service.service canalysis-service.service media-analytics-service.service kpi-logging.service
ROLLBACK
chmod 700 "$backup/rollback.sh"
systemctl cat adagent-service.service pisa-service.service canalysis-service.service media-analytics-service.service kpi-logging.service > "$backup/units-before.txt"
systemctl show adagent-service.service pisa-service.service canalysis-service.service media-analytics-service.service kpi-logging.service -p Id -p ActiveState -p UnitFileState > "$backup/state-before.txt"
trap 'sh "$backup/rollback.sh"; echo APPLY_FAILED_ROLLED_BACK' 0
: > "$base/pisa-service"
: > "$base/canalysis-daemon"
ln -s /dev/null /run/systemd/system/media-analytics-service.service
ln -s /dev/null /run/systemd/system/kpi-logging.service
systemctl daemon-reload
systemctl stop adagent-service.service pisa-service.service canalysis-service.service media-analytics-service.service kpi-logging.service
for u in adagent-service.service pisa-service.service canalysis-service.service media-analytics-service.service kpi-logging.service; do
 state=$(systemctl show "$u" -p ActiveState --value)
 echo "$u=$state"
 [ "$state" = inactive ] || exit 1
done
trap - 0
echo APPLY_OK
