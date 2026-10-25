"""Install the optional TV backend on the explicitly configured monitor.
Only a runtime systemd unit is created: no firmware remount or hidden boot hook.
"""
import sys, os, json, secrets, tarfile, subprocess, shutil, shlex
from pathlib import Path
HERE=Path(__file__).resolve().parent
sys.path.insert(0,str(HERE.parent))
from tv import TV,SDB,HOST
state=HERE.parent/'.state/tv-service';state.mkdir(parents=True,exist_ok=True,mode=0o700)
if not (state/'control-token').exists():
 (state/'control-token').write_text(secrets.token_urlsafe(32));(state/'control-token').chmod(0o600)
(state/'host').write_text(HOST)
(state/'launcher-config.js').write_text('window.DECK_DEVICE='+json.dumps({'url':'http://127.0.0.1:8844','token':(state/'control-token').read_text().strip()})+';')
if not (state/'server.pfx').exists():
 subprocess.run(['openssl','req','-x509','-newkey','rsa:2048','-nodes','-keyout',str(state/'server.key'),'-out',str(state/'server.crt'),'-days','365','-subj','/CN=Tizen Deck Monitor','-addext','subjectAltName=IP:'+HOST],check=True,stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL)
 subprocess.run(['openssl','pkcs12','-export','-legacy','-out',str(state/'server.pfx'),'-inkey',str(state/'server.key'),'-in',str(state/'server.crt'),'-passout','pass:'],check=True)
for f in state.iterdir():f.chmod(0o700 if f.is_dir() else 0o600)
archive=state/'install.tar.gz'
with tarfile.open(archive,'w:gz') as t:
 for p in (HERE/'dist').rglob('*'):
  if p.is_file():t.add(p,arcname=p.relative_to(HERE/'dist'))
 for name in ['host','control-token','server.pfx'] + (['update-public.der'] if (state/'update-public.der').exists() else []):t.add(state/name,arcname='state/'+name)
 t.add(HERE/'deck-tv.service',arcname='deck-tv.service')
remote='/home/owner/share/tmp/sdk_tools/deck-tv-install.tar.gz'
subprocess.run([str(SDB),'connect',HOST+':26101'],check=True)
subprocess.run([str(SDB),'-s',HOST+':26101','push',str(archive),remote],check=True)
suffix=secrets.token_hex(6)
next_dir='/opt/usr/tizen-deck.next-'+suffix
backup='/opt/usr/tizen-deck.previous-'+suffix
command=f"""set -e
umask 077
mkdir {next_dir}
tar -xzf {remote} -C {next_dir}
if [ -f /opt/usr/tizen-deck/state/auth.json ]; then cp /opt/usr/tizen-deck/state/auth.json {next_dir}/state/auth.json; fi
# Keep the originally enrolled update trust key on an upgrade.
if [ -f /opt/usr/tizen-deck/state/update-public.der ]; then cp /opt/usr/tizen-deck/state/update-public.der {next_dir}/state/update-public.der; fi
if [ -d /opt/usr/tizen-deck/updates ]; then cp -a /opt/usr/tizen-deck/updates {next_dir}/updates; fi
chown -R 0:0 {next_dir}
chmod 700 {next_dir}/state
chmod 600 {next_dir}/state/*
systemctl stop deck-tv.service 2>/dev/null || true
if [ -d /opt/usr/tizen-deck ]; then mv /opt/usr/tizen-deck {backup}; fi
mv {next_dir} /opt/usr/tizen-deck
cp /opt/usr/tizen-deck/deck-tv.service /run/systemd/system/deck-tv.service
systemctl daemon-reload
if systemctl start deck-tv.service && systemctl is-active --quiet deck-tv.service; then
 echo active
 echo 'Previous installation: {backup}'
else
 systemctl stop deck-tv.service 2>/dev/null || true
 mv /opt/usr/tizen-deck {next_dir}.failed
 if [ -d {backup} ]; then
  mv {backup} /opt/usr/tizen-deck
  cp /opt/usr/tizen-deck/deck-tv.service /run/systemd/system/deck-tv.service
  systemctl daemon-reload
  systemctl start deck-tv.service || true
 fi
 echo 'Update failed; previous installation restored when available' >&2
 exit 1
fi
rm -f {remote}
"""
r=TV().root(command);print(r['output'])
if r['exit_code'] or 'active' not in r['output'].splitlines():raise SystemExit('Native TV service did not start')
print('Local controller installed. Existing credentials and HTTPS setting preserved; a new installation starts disabled.')
print('Runtime unit only: cold-boot startup is not installed.')
