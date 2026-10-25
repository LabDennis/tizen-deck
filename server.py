#!/usr/bin/env python3
"""Password protected local TV management UI; binds loopback only."""
import argparse, re, hashlib, hmac, json, secrets, threading, time, traceback
from concurrent.futures import ThreadPoolExecutor
from http.cookies import SimpleCookie
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import urlparse
from tv import TV, HERE, STATE, HOST, APPS
from wake import WakeOnLan


def password_hash(password, salt):
    return hashlib.scrypt(password.encode(),salt=bytes.fromhex(salt),n=16384,r=8,p=1).hex()

def save_password(password, username=None):
    if username is None:
        username=json.loads((STATE/"auth.json").read_text()).get("username","admin") if (STATE/"auth.json").exists() else "admin"
    if not isinstance(username,str) or not re.fullmatch(r"[A-Za-z0-9_.-]{3,40}",username):raise ValueError("Benutzername: 3–40 Buchstaben, Zahlen, Punkt, Bindestrich oder Unterstrich.")
    if len(password)<12: raise ValueError('Mindestens 12 Zeichen verwenden.')
    salt=secrets.token_hex(16)
    p=STATE/'auth.json'; p.write_text(json.dumps({'username':username,'salt':salt,'hash':password_hash(password,salt)}));p.chmod(0o600)

def init_auth():
    if not (STATE/'auth.json').exists():
        password=secrets.token_urlsafe(18);save_password(password)
        p=STATE/'initial-login.txt';p.write_text('Benutzer: admin\nPasswort: '+password+'\n');p.chmod(0o600)

class App:
    def __init__(self):
        self.tv=TV();self.sessions={};self.jobs={};self.lock=threading.Lock();self.failures=[]
        self.wake=WakeOnLan(STATE)
        self.pool=ThreadPoolExecutor(max_workers=1)
    def login(self,user,password):
        now=time.time()
        with self.lock:
            self.failures=[t for t in self.failures if now-t<60]
            if len(self.failures)>=6: raise ValueError('Zu viele Versuche. Bitte eine Minute warten.')
            auth=json.loads((STATE/'auth.json').read_text())
            valid=hmac.compare_digest(password_hash(password,auth['salt']),auth['hash']) and hmac.compare_digest(user,auth.get('username','admin'))
            if not valid: self.failures.append(now);raise ValueError('Benutzername oder Passwort falsch.')
            self.sessions={k:v for k,v in self.sessions.items() if v['expires']>now}
            token=secrets.token_urlsafe(32);session={'csrf':secrets.token_urlsafe(24),'expires':now+8*3600}
            self.sessions[token]=session
            return token,session
    def submit(self,fn):
        with self.lock:
            active=sum(j['status'] in ('queued','running') for j in self.jobs.values())
            if active>=4: raise ValueError('Warteschlange voll. Laufenden Auftrag abwarten.')
            for key in list(self.jobs):
                if time.time()-self.jobs[key]['created']>3600: del self.jobs[key]
            key=secrets.token_hex(12);self.jobs[key]={'status':'queued','created':time.time()}
        def run():
            self.jobs[key]['status']='running'
            try: self.jobs[key].update(status='done',result=fn())
            except Exception as e: self.jobs[key].update(status='error',error=str(e)[:1500])
        self.pool.submit(run);return key

APP=None
class Handler(BaseHTTPRequestHandler):
    def log_message(self,fmt,*args): pass
    def reply(self,status,data,headers=None):
        content=json.dumps(data,ensure_ascii=False).encode() if isinstance(data,(dict,list)) else data
        self.send_response(status)
        self.send_header('Content-Type',(headers or {}).get('Content-Type','application/json; charset=utf-8' if isinstance(data,(dict,list)) else 'application/octet-stream'))
        self.send_header('Content-Length',str(len(content)))
        self.send_header('Cache-Control','no-store')
        self.send_header('X-Content-Type-Options','nosniff')
        self.send_header('X-Frame-Options','DENY')
        for k,v in (headers or {}).items():
            if k != 'Content-Type': self.send_header(k,v)
        self.end_headers();self.wfile.write(content)
    def host_ok(self):
        return self.headers.get('Host') in (f'127.0.0.1:{self.server.server_port}',f'localhost:{self.server.server_port}')
    def session(self):
        cookie=SimpleCookie()
        try:cookie.load(self.headers.get('Cookie',''))
        except Exception:return None,None
        token=cookie.get('deck_session');token=token.value if token else None
        session=APP.sessions.get(token)
        return (token,session) if session and session['expires']>time.time() else (None,None)
    def do_GET(self):
        if not self.host_ok():return self.reply(403,{'error':'Ungültiger Host'})
        path=urlparse(self.path).path
        if path in ('/','/app.js','/input.js','/style.css'):
            name={'/':'index.html','/app.js':'app.js','/input.js':'input.js','/style.css':'style.css'}[path]
            data=(HERE/'static'/name).read_bytes();kind={'/':'text/html','/app.js':'text/javascript','/input.js':'text/javascript','/style.css':'text/css'}[path]
            return self.reply(200,data,{'Content-Type':kind+'; charset=utf-8','Content-Security-Policy':"default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; connect-src 'self'; frame-ancestors 'none'; form-action 'self'"})
        _,s=self.session()
        if not s:return self.reply(401,{'error':'Bitte anmelden.'})
        if path=='/api/session':return self.reply(200,{'csrf':s['csrf'],'host':HOST,'apps':APPS,'username':json.loads((STATE/'auth.json').read_text()).get('username','admin')})
        if path=='/api/wake-config':return self.reply(200,APP.wake.config())
        if path=='/api/screen':
            screen=APP.tv.screen
            if screen is None:return self.reply(404,{'error':'Noch keine Aufnahme vorhanden.'})
            return self.reply(200,screen[0],{'Content-Type':'image/png'})
        if path=='/api/device':
            try:return self.reply(200,{'device':APP.tv.device(),'time':time.time()})
            except Exception as e:return self.reply(503,{'error':str(e)})
        if path.startswith('/api/jobs/'):
            job=APP.jobs.get(path.rsplit('/',1)[-1])
            return self.reply(200 if job else 404,job or {'error':'Auftrag nicht gefunden'})
        return self.reply(404,{'error':'Nicht gefunden'})
    def do_POST(self):
        if not self.host_ok():return self.reply(403,{'error':'Ungültiger Host'})
        expected='http://'+self.headers['Host']
        if self.headers.get('Origin')!=expected:return self.reply(403,{'error':'Ungültiger Ursprung'})
        if not self.headers.get('Content-Type','').startswith('application/json'):return self.reply(415,{'error':'JSON erwartet'})
        try:
            size=int(self.headers.get('Content-Length','0'))
            if not 0<size<=12000: return self.reply(413,{'error':'Anfrage zu groß oder leer'})
            data=json.loads(self.rfile.read(size))
            if not isinstance(data,dict):raise ValueError('Objekt erwartet')
        except Exception:return self.reply(400,{'error':'Ungültige Anfrage'})
        path=urlparse(self.path).path
        try:
            if path=='/api/login':
                u,p=data.get('username',''),data.get('password','')
                if not isinstance(u,str) or not isinstance(p,str):raise ValueError('Ungültige Anmeldung')
                token,s=APP.login(u,p)
                return self.reply(200,{'csrf':s['csrf']},{'Set-Cookie':f'deck_session={token}; HttpOnly; SameSite=Strict; Path=/; Max-Age=28800'})
            token,s=self.session()
            if not s:return self.reply(401,{'error':'Bitte anmelden.'})
            if not hmac.compare_digest(self.headers.get('X-CSRF-Token',''),s['csrf']):return self.reply(403,{'error':'Ungültige Sitzung'})
            if path=='/api/logout':
                APP.sessions.pop(token,None);return self.reply(200,{'ok':True},{'Set-Cookie':'deck_session=; HttpOnly; SameSite=Strict; Path=/; Max-Age=0'})
            if path=='/api/password':
                old=data.get('current','');new=data.get('password','')
                auth=json.loads((STATE/'auth.json').read_text())
                if not hmac.compare_digest(password_hash(old,auth['salt']),auth['hash']):raise ValueError('Aktuelles Passwort falsch')
                save_password(new,data.get('username'));APP.sessions={token:s};(STATE/'initial-login.txt').unlink(missing_ok=True)
                return self.reply(200,{'message':'Zugang geändert; andere Sitzungen beendet.'})
            if path=='/api/input':
                if not any(k in data for k in ('key','text','mouse')):raise ValueError('Eingabe fehlt')
                return self.reply(200,APP.tv.remote(key=data.get('key'),text=data.get('text'),mouse=data.get('mouse')))
            if path=='/api/wake-config':
                return self.reply(200,APP.wake.save(data.get('mac'),data.get('broadcast')))
            if path=='/api/read':fn=lambda:APP.tv.read(data.get('kind'))
            elif path=='/api/wake':fn=lambda:APP.wake.wake(APP.tv.device)
            elif path=='/api/capture':fn=APP.tv.capture_screen
            elif path=='/api/launch':fn=lambda:APP.tv.launch(data.get('app'))
            elif path=='/api/remote':fn=lambda:APP.tv.remote(key=data.get('key'),text=data.get('text'))
            elif path=='/api/update':fn=lambda:APP.tv.update(data.get('enabled'))
            elif path=='/api/console':
                command=data.get('command');mode=data.get('mode')
                if not isinstance(command,str) or not 1<=len(command)<=4000 or '\x00' in command:raise ValueError('Befehl: 1–4000 Zeichen')
                if mode not in ('sdk','root'):raise ValueError('Ungültiger Konsolenmodus')
                if mode=='root' and data.get('confirm_root') is not True:raise ValueError('Root-Modus muss bewusst bestätigt werden')
                fn=lambda:APP.tv.root(command) if mode=='root' else APP.tv.sdk(command)
            else:return self.reply(404,{'error':'Nicht gefunden'})
            return self.reply(202,{'job':APP.submit(fn)})
        except ValueError as e:return self.reply(400,{'error':str(e)})
        except Exception:return self.reply(500,{'error':'Interner Fehler. Bitte erneut versuchen.'})

def main():
    global APP
    parser=argparse.ArgumentParser();parser.add_argument('--port',type=int,default=8765);parser.add_argument('--set-password',action='store_true');args=parser.parse_args()
    init_auth()
    if args.set_password:
        import getpass
        p=getpass.getpass('Neues Passwort: ')
        if p!=getpass.getpass('Wiederholen: '):raise SystemExit('Passwörter stimmen nicht überein')
        save_password(p);(STATE/'initial-login.txt').unlink(missing_ok=True);return
    APP=App();server=ThreadingHTTPServer(('127.0.0.1',args.port),Handler)
    print(f'Tizen Deck: http://localhost:{args.port} — Zugangsdaten: {STATE / "initial-login.txt"}',flush=True)
    try:server.serve_forever()
    except KeyboardInterrupt:pass
    finally:server.server_close();APP.pool.shutdown(wait=False,cancel_futures=True)
if __name__=='__main__':main()
