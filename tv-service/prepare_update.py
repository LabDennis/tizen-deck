"""Sign and optionally stage a launcher update; never wakes or launches the TV."""
import argparse,hashlib,json,subprocess,sys,tarfile,xml.etree.ElementTree as ET,zipfile
from pathlib import Path
HERE=Path(__file__).resolve().parent
sys.path.insert(0,str(HERE.parent))

def prepare(package,state,out):
 if not package.is_file() or not 100<=package.stat().st_size<=33554432:raise ValueError('WGT size invalid')
 with zipfile.ZipFile(package) as z:
  if sum(i.filename=='config.xml' for i in z.infolist())!=1:raise ValueError('config.xml must be unique')
  if z.getinfo('config.xml').file_size>65536:raise ValueError('config.xml too large')
  root=ET.fromstring(z.read('config.xml'))
  app=root.find('{http://tizen.org/ns/widgets}application')
  if app is None or app.get('id')!='TizenDeckX.XMB' or app.get('package')!='TizenDeckX':raise ValueError('Not a Tizen Deck launcher package')
  version=root.get('version')
 import re
 if not re.fullmatch(r'[0-9]{1,4}\.[0-9]{1,4}\.[0-9]{1,4}',version or ''):raise ValueError('Invalid version')
 state.mkdir(parents=True,exist_ok=True,mode=0o700);out.mkdir(parents=True,exist_ok=True,mode=0o700)
 key=state/'update-private.pem';public=state/'update-public.der'
 if not key.exists():
  subprocess.run(['openssl','genpkey','-algorithm','RSA','-pkeyopt','rsa_keygen_bits:3072','-out',str(key)],check=True,stderr=subprocess.DEVNULL);key.chmod(0o600)
 subprocess.run(['openssl','pkey','-in',str(key),'-pubout','-outform','DER','-out',str(public)],check=True)
 manifest={'component':'TizenDeckX.XMB','version':version,'sha256':hashlib.sha256(package.read_bytes()).hexdigest()}
 (out/'manifest.json').write_text(json.dumps(manifest,sort_keys=True,separators=(',',':')))
 (out/'launcher.wgt').write_bytes(package.read_bytes())
 subprocess.run(['openssl','dgst','-sha256','-sign',str(key),'-out',str(out/'manifest.sig'),str(out/'manifest.json')],check=True)
 for f in out.iterdir():f.chmod(0o600)
 return public

def main():
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('package',type=Path);p.add_argument('--stage',action='store_true');p.add_argument('--baseline',action='store_true',help='Save this exact installed version as rollback baseline');a=p.parse_args()
 state=HERE.parent/'.state/tv-service';out=state/'pending-update';public=prepare(a.package,state,out)
 print('Signed update:',out)
 if not (a.stage or a.baseline):return
 from tv import TV,SDB,HOST
 # Trust key must already be provisioned by install_tv.py, never by this update.
 archive=state/'pending-update.tar.gz'
 with tarfile.open(archive,'w:gz') as t:
  for name in ('manifest.json','manifest.sig','launcher.wgt'):t.add(out/name,arcname=name)
 remote='/home/owner/share/tmp/sdk_tools/deck-update.tar.gz'
 subprocess.run([str(SDB),'connect',HOST+':26101'],check=True)
 subprocess.run([str(SDB),'-s',HOST+':26101','push',str(archive),remote],check=True)
 slot='last-installed' if a.baseline else 'pending'
 guard=''
 if a.baseline:
  version=json.loads((out/'manifest.json').read_text())['version']
  guard="test \"$(sqlite3 /opt/dbspace/.pkgmgr_parser.db \"select package_version from package_info where package='TizenDeckX';\")\" = \""+version+"\"; "
 r=TV().root('set -e; '+guard+'test -f /opt/usr/tizen-deck/state/update-public.der; umask 077; mkdir -p /opt/usr/tizen-deck/updates/incoming; tar -xzf '+remote+' -C /opt/usr/tizen-deck/updates/incoming; rm -rf /opt/usr/tizen-deck/updates/'+slot+'; mv /opt/usr/tizen-deck/updates/incoming /opt/usr/tizen-deck/updates/'+slot+'; rm -f '+remote)
 if r['exit_code']:raise RuntimeError(r['output'])
 print('Installed-version baseline saved.' if a.baseline else 'Staged only. Install via Settings → Tizen-Deck-Updates on the monitor.')
if __name__=='__main__':main()
