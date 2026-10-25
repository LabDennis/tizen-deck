"""Local bridge to an owner-authorized Samsung monitor. No boot persistence."""
import contextlib, fcntl
import asyncio, base64, json, os, re, shlex, ssl, sys, threading, time
from pathlib import Path
from urllib.request import build_opener, ProxyHandler
from urllib.parse import urlencode

HERE = Path(__file__).resolve().parent
WORK = HERE / '.deps'
sys.path.insert(0, str(WORK / 'samsung-tv-root/src'))
from samsung_tv_root.sdb import SdbClient, route_callback_host
from samsung_tv_root import archive_root
from samsung_tv_root.root_agent import RootAgentServer

CONFIG = json.loads((HERE / '.state/config.json').read_text()) if (HERE / '.state/config.json').exists() else {}
HOST = os.environ.get('TIZEN_HOST', CONFIG.get('tv_host', '127.0.0.1'))
SDB = WORK / 'tizen-sdb/data/tools/sdb'
os.environ['SDB'] = str(SDB)
STATE = HERE / '.state'
STATE.mkdir(mode=0o700, exist_ok=True)
KEYS = {'KEY_UP','KEY_DOWN','KEY_LEFT','KEY_RIGHT','KEY_ENTER','KEY_RETURN','KEY_HOME','KEY_MENU','KEY_SOURCE','KEY_VOLUP','KEY_VOLDOWN','KEY_MUTE','KEY_PLAY','KEY_PAUSE','KEY_STOP','KEY_HDMI','KEY_POWER','KEY_BACKSPACE','KEY_TAB'}
APPS = {
 'plex': ('Plex', 'kIciSQlYEM.plex'),
 'moonlight': ('Moonlight', 'MoonLightS.MoonlightWasm'),
 'youtube': ('YouTube', '9Ur5IzDKqV.TizenYouTube'),
 'browser': ('Internet', 'org.tizen.browser'),
}
READS = {
 'metrics': "echo SECTION:uptime; cat /proc/uptime; echo SECTION:load; cat /proc/loadavg; echo SECTION:memory; cat /proc/meminfo; echo SECTION:disk; df -k /opt; echo SECTION:update; vconftool get db/menu/support/softwareupdate/autoupdate; echo SECTION:identity; id",
 'processes': 'ps -eo pid,ppid,user,stat,comm,args | head -160',
 'services': 'systemctl list-units --type=service --all --no-pager --no-legend',
 'packages': 'pkgcmd -l -t tpk; pkgcmd -l -t wgt',
 'logs': 'journalctl -n 100 --no-pager -o short-iso',
 'network': 'cat /proc/net/dev; ss -tun 2>/dev/null || netstat -tn 2>/dev/null',
 'capabilities': 'id; uname -r; cat /proc/self/attr/current; command -v enlightenment_info; command -v screenshot',
}

def sections(raw):
    result = {}
    for block in raw.split('SECTION:')[1:]:
        key, _, body = block.partition('\n'); result[key.strip()] = body.strip()
    return result

def metrics(raw):
    s = sections(raw); memory = {}
    for line in s.get('memory','').splitlines():
        m = re.match(r'(\w+):\s+(\d+)', line)
        if m: memory[m[1]] = int(m[2])
    total = memory.get('MemTotal', 0)
    available = memory.get('MemAvailable', memory.get('MemFree', 0))
    disk = s.get('disk','').splitlines()
    fields = disk[-1].split() if len(disk)>1 else []
    match = re.search(r'value = ([01])', s.get('update',''))
    return {'uptime': s.get('uptime','').split(' ')[0], 'load': s.get('load','').split()[:3],
            'memory_total_kb':total, 'memory_used_kb':max(0,total-available),
            'disk_used_kb':int(fields[2]) if len(fields)>=5 and fields[2].isdigit() else None,
            'disk_total_kb':int(fields[1]) if len(fields)>=5 and fields[1].isdigit() else None,
            'auto_update':bool(int(match[1])) if match else None,'identity':s.get('identity',''), 'raw':raw}

class Client(SdbClient):
    def capture(self, command, **kw):
        cb = route_callback_host(self.tv_host)
        kw.update(callback_host=cb, bind_host=cb, port=1764)
        return super().capture(command, **kw)

class Agent(RootAgentServer):
    def __init__(self, host, port, *a, **kw): super().__init__(host,1763,*a,**kw)

@contextlib.contextmanager
def bridge_lock():
    path='/tmp/tizen-deck-'+str(os.getuid())+'-'+HOST+'.lock'
    fd=os.open(path,os.O_CREAT|os.O_RDWR|os.O_NOFOLLOW,0o600)
    try:
        fcntl.flock(fd,fcntl.LOCK_EX)
        yield
    finally:
        fcntl.flock(fd,fcntl.LOCK_UN);os.close(fd)

class TV:
    def __init__(self):
        self.lock=threading.Lock(); self.remote_lock=threading.Lock(); self.ws=None; self.screen=None
    def device(self):
        with build_opener(ProxyHandler({})).open(f'http://{HOST}:8001/api/v2/',timeout=4) as r:
            d=json.load(r).get('device',{})
        return {k:d.get(k) for k in ('name','modelName','PowerState','developerMode','developerIP','resolution')}
    def sdk(self, command):
        with self.lock, bridge_lock():
            c=Client(SDB,HOST,timeout=10); c.connect(); c.require_device()
            result=c.capture(command,timeout=25).output
        m=re.search(r'\n\[exit:(\d+)\]\s*$',result)
        if not m: raise RuntimeError('Keine vollständige Antwort vom Monitor erhalten.')
        return {'output':result[:m.start()], 'exit_code':int(m[1]), 'mode':'sdk', 'time':time.time()}
    def root(self, command):
        with self.lock, bridge_lock():
            archive_root.SdbClient=Client; archive_root.RootAgentServer=Agent
            callback=route_callback_host(HOST)
            result=asyncio.run(archive_root.probe(HOST,model='LS32BM700UUXEN',callback_host=callback,bind_host=callback,
                commands=(command,),command_timeout=30,
                payloads=WORK/'root-release/samsung-tv-root-0.0.12-linux-x86_64/_internal/payloads/common31'))
        cmd=result['commands'][0]
        return {'output':cmd['stdout']+cmd['stderr'],'exit_code':cmd['exit_code'],'timed_out':cmd['timed_out'],
                'mode':'root','uid':result['uid'],'smack':result['smack'],'cleaned':result['staging_cleaned'],
                'system_files_unchanged':result['system_files_unchanged'],'time':time.time()}
    def capture_screen(self):
        # A unique temporary capture; no persistent root or public TV file server.
        import secrets
        path='/tmp/deck-screen-'+secrets.token_hex(8)+'.png'
        command=("umask 077; trap 'rm -f "+path+"' EXIT; "
                 "enlightenment_info -dump_screen -p /tmp/ -n "+path.rsplit('/',1)[1]+
                 " >/dev/null 2>&1; test -s "+path+" || exit 1; "
                 "test $(wc -c < "+path+") -le 8388608 || exit 2; base64 "+path)
        result=self.root(command)
        if result['exit_code'] or result['timed_out']:
            raise RuntimeError('Bildschirmaufnahme fehlgeschlagen; bisheriges Bild ist unverändert.')
        try:
            frame=base64.b64decode(''.join(result['output'].split()),validate=True)
        except ValueError as error:
            raise RuntimeError('Ungültige Bildschirmdaten') from error
        if not frame.startswith(b'\x89PNG\r\n\x1a\n') or len(frame)>8388608:
            raise RuntimeError('Ungültiges PNG vom Monitor')
        captured=time.time()
        self.screen=(frame,captured)
        return {'captured_at':captured,'message':'Bildschirmaufnahme aktualisiert.'}
    def launch(self, name):
        if name not in APPS: raise ValueError('Unbekannte App')
        return self.sdk('app_launcher -s '+shlex.quote(APPS[name][1]))
    def read(self, kind):
        if kind not in READS: raise ValueError('Unbekannte Diagnose')
        r=self.sdk(READS[kind])
        if kind=='metrics': r['metrics']=metrics(r['output'])
        return r
    def update(self, enabled):
        if type(enabled) is not bool: raise ValueError('enabled muss bool sein')
        return self.root('vconftool set -f -t int db/menu/support/softwareupdate/autoupdate '+str(int(enabled))+' && vconftool get db/menu/support/softwareupdate/autoupdate')
    def connect_remote(self):
        import websocket
        if self.ws and self.ws.connected: return
        token_path=STATE/'tv-token'
        params={'name':base64.b64encode(b'Tizen Deck').decode()}
        if token_path.exists(): params['token']=token_path.read_text().strip()
        ws=websocket.create_connection(f'wss://{HOST}:8002/api/v2/channels/samsung.remote.control?'+urlencode(params),timeout=15,
            sslopt={'cert_reqs':ssl.CERT_NONE},http_no_proxy=[HOST],suppress_origin=True)
        try:
            for _ in range(12):
                msg=json.loads(ws.recv()); event=msg.get('event')
                if event=='ms.channel.unauthorized': raise RuntimeError('Am Monitor den Zugriff für Tizen Deck erlauben.')
                if event=='ms.channel.connect':
                    token=msg.get('data',{}).get('token')
                    if token:
                        token_path.write_text(str(token)); token_path.chmod(0o600)
                    self.ws=ws; return
            raise RuntimeError('Fernbedienung nicht bestätigt. Bitte am Monitor erlauben.')
        except Exception:
            ws.close(); raise
    def remote(self,key=None,text=None,mouse=None):
        if sum(x is not None for x in (key,text,mouse))>1: raise ValueError("Nur eine Eingabe pro Anfrage")
        if mouse is not None:
            if not isinstance(mouse,dict) or set(mouse)!={"x","y"} or any(type(mouse[k]) is not int or not 0<=mouse[k]<=8192 for k in mouse): raise ValueError("Ungültige Mausposition")
        if key is not None and key not in KEYS: raise ValueError('Unbekannte Taste')
        if text is not None and (not isinstance(text,str) or not 1<=len(text)<=500): raise ValueError('Text: 1–500 Zeichen')
        with self.remote_lock:
            self.connect_remote()
            if key: params={'Cmd':'Click','DataOfCmd':key,'Option':'false','TypeOfRemote':'SendRemoteKey'}
            elif mouse is not None: params={'Cmd':'Move','Position':{'x':mouse['x'],'y':mouse['y'],'Time':'0'},'TypeOfRemote':'ProcessMouseDevice'}
            elif text: params={'Cmd':base64.b64encode(text.encode()).decode(),'DataOfCmd':'base64','TypeOfRemote':'SendInputString'}
            else: return {'message':'Fernbedienung gekoppelt.'}
            try: self.ws.send(json.dumps({'method':'ms.remote.control','params':params}))
            except Exception:
                self.ws.close(); self.ws=None; raise RuntimeError('Verbindung unterbrochen. Erneut koppeln.')
        return {'message':'Befehl an den Monitor gesendet; Ausführung nicht quittiert.'}
