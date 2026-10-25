set -eu
umask 077
ROOT=/opt/usr/tizen-deck
DEPOT=/opt/usr/tizen-deck-updates
NEXT=NEXT_PATH
BACKUP=BACKUP_PATH
JOB=JOB_PATH
committed=0
result() { printf '%s' "$1" > "$DEPOT/result.json.new"; mv "$DEPOT/result.json.new" "$DEPOT/result.json"; }
healthy() {
 for attempt in 1 2 3 4 5 6 7 8; do
  sleep 1
  if systemctl is-active --quiet deck-tv.service && curl --fail --silent --show-error --connect-timeout 1 --max-time 3 -H "X-Deck-Control: $(cat "$ROOT/state/control-token")" http://127.0.0.1:8844/health > "$JOB/health.json"; then
   if grep -Fq '"ready":true' "$JOB/health.json" && grep -Fq "\"version\":\"$1\"" "$JOB/health.json"; then return 0; fi
  fi
 done
 return 1
}
restore() {
 rc=$?
 trap - EXIT HUP INT TERM
 if [ "$committed" != 1 ]; then
  if [ -d "$BACKUP" ]; then
   systemctl stop deck-tv.service 2>/dev/null || true
   [ ! -d "$ROOT" ] || mv "$ROOT" "$NEXT.failed"
   mv "$BACKUP" "$ROOT"
   cp "$ROOT/deck-tv.service" /run/systemd/system/deck-tv.service
   systemctl daemon-reload
   systemctl reset-failed deck-tv.service 2>/dev/null || true
   systemctl start deck-tv.service || true
   if healthy OLD_VERSION; then
    result '{"phase":"rolled_back","message":"Backend-Update fehlgeschlagen; vorheriger Dienst wieder aktiv"}'
   else
    result '{"phase":"recovery_required","message":"Vorherige Dateien wiederhergestellt; Dienststart nicht bestätigt"}'
   fi
  else
   systemctl start deck-tv.service 2>/dev/null || true
   result '{"phase":"failed","message":"Vor dem Dateitausch abgebrochen; Originaldateien erhalten"}'
  fi
 fi
 exit "$rc"
}
trap restore EXIT HUP INT TERM
sleep 1
systemctl stop deck-tv.service
cp -a "$ROOT/state" "$NEXT/state"
[ ! -d "$ROOT/updates" ] || cp -a "$ROOT/updates" "$NEXT/updates"
chmod 700 "$NEXT/state"
mv "$ROOT" "$BACKUP"
mv "$NEXT" "$ROOT"
cp "$ROOT/deck-tv.service" /run/systemd/system/deck-tv.service
systemctl daemon-reload
systemctl reset-failed deck-tv.service 2>/dev/null || true
systemctl start deck-tv.service
healthy NEW_VERSION
mv "$JOB/previous.json" "$DEPOT/previous.json"
result '{"phase":"complete","version":"NEW_VERSION","message":"Backend-Version und Dienststart geprüft"}'
committed=1
