import sys, threading, unittest, json, urllib.request, urllib.error, tempfile
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parents[1]))
import server
from tv import metrics
class Tests(unittest.TestCase):
 @classmethod
 def setUpClass(cls):
  cls.temp=tempfile.TemporaryDirectory();cls.original=server.STATE;server.STATE=Path(cls.temp.name)
  server.save_password('test-password-123456');server.APP=server.App()
  cls.http=server.ThreadingHTTPServer(('127.0.0.1',0),server.Handler);cls.port=cls.http.server_port
  threading.Thread(target=cls.http.serve_forever,daemon=True).start()
 @classmethod
 def tearDownClass(cls):
  cls.http.shutdown();cls.http.server_close();server.APP.pool.shutdown();server.STATE=cls.original;cls.temp.cleanup()
 def request(self,path,data=None,cookie=None,csrf=None,origin=True,host=None):
  headers={}
  if data is not None:headers.update({'Content-Type':'application/json','Origin':f'http://127.0.0.1:{self.port}' if origin else 'http://evil.test'})
  if cookie:headers['Cookie']=cookie
  if csrf:headers['X-CSRF-Token']=csrf
  if host:headers['Host']=host
  req=urllib.request.Request(f'http://127.0.0.1:{self.port}'+path,data=json.dumps(data).encode() if data is not None else None,headers=headers)
  try:r=urllib.request.urlopen(req)
  except urllib.error.HTTPError as e:r=e
  body=r.read();return r.status,r.headers,json.loads(body) if r.headers.get_content_type()=='application/json' else body
 def login(self):
  status,h,b=self.request('/api/login',{'username':'admin','password':'test-password-123456'})
  self.assertEqual(status,200);return h['Set-Cookie'].split(';')[0],b['csrf']
 def test_auth_required(self):
  for path in ['/api/session','/api/device','/api/jobs/anything']:
   self.assertEqual(self.request(path)[0],401)
  self.assertEqual(self.request('/api/console',{'command':'id','mode':'root'})[0],401)
 def test_wrong_password(self):self.assertEqual(self.request('/api/login',{'username':'admin','password':'wrong'})[0],400)
 def test_cross_origin_and_dns_rebinding(self):
  self.assertEqual(self.request('/api/login',{'username':'admin','password':'test-password-123456'},origin=False)[0],403)
  self.assertEqual(self.request('/api/session',host='evil.test')[0],403)
 def test_csrf_and_root_opt_in(self):
  cookie,csrf=self.login()
  self.assertEqual(self.request('/api/console',{'command':'id','mode':'root'},cookie=cookie)[0],403)
  self.assertEqual(self.request('/api/console',{'command':'id','mode':'root'},cookie=cookie,csrf=csrf)[0],400)
 def test_console_job_routes_without_real_tv(self):
  cookie,csrf=self.login();old=server.APP.tv.sdk;server.APP.tv.sdk=lambda command:{'output':'uid=901','exit_code':0}
  try:
   status,_,b=self.request('/api/console',{'command':'id','mode':'sdk'},cookie=cookie,csrf=csrf)
   self.assertEqual(status,202);self.assertIn('job',b)
  finally:server.APP.pool.submit(lambda:None).result();server.APP.tv.sdk=old
 def test_logout_revokes_session(self):
  cookie,csrf=self.login();self.request('/api/logout',{},cookie=cookie,csrf=csrf)
  self.assertEqual(self.request('/api/session',cookie=cookie)[0],401)
 def test_static_not_private_files(self):
  self.assertEqual(self.request('/.state/auth.json')[0],401)
  status,h,_=self.request('/');self.assertEqual(status,200);self.assertEqual(h.get_content_type(),'text/html')
 def test_metrics_missing_and_real(self):
  self.assertIsNone(metrics('')['auto_update'])
  m=metrics('SECTION:memory\nMemTotal: 1000 kB\nMemAvailable: 250 kB\nSECTION:disk\nFilesystem 1K Used Available Use Mounted\n/dev/test 2000 500 1500 25% /opt\nSECTION:update\ndb/menu/support/softwareupdate/autoupdate, value = 0 (Int32)\n')
  self.assertEqual(m['memory_used_kb'],750);self.assertEqual(m['disk_used_kb'],500);self.assertFalse(m['auto_update'])
if __name__=='__main__':unittest.main()
