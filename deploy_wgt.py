"""Deploy an explicitly selected local package via an opted-in temporary root session."""
import argparse,shlex,subprocess,sys
from pathlib import Path
from tv import TV,SDB,HOST
p=argparse.ArgumentParser();p.add_argument('package',type=Path);p.add_argument('--allow-root',action='store_true');a=p.parse_args()
if not a.allow_root:p.error('Temporary root deployment requires --allow-root')
if not a.package.is_file() or a.package.suffix!='.wgt':p.error('Existing .wgt file required')
remote='/home/owner/share/tmp/sdk_tools/deck-install.wgt'
subprocess.run([str(SDB),'connect',HOST+':26101'],check=True)
subprocess.run([str(SDB),'-s',HOST+':26101','push',str(a.package.resolve()),remote],check=True)
r=TV().root('pkgcmd -i -t wgt -p '+shlex.quote(remote));print(r['output'])
if r['exit_code'] or 'val[ok]' not in r['output']:raise SystemExit('Installation failed')
TV().sdk('rm -f '+shlex.quote(remote))
print('Installed; no autostart or permanent root configured.')
