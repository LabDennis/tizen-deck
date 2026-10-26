import hashlib
import http.client
import json
from pathlib import Path
import ssl
import sys
import tempfile
import threading
import unittest
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import release_server as feed


class ReleaseServerTests(unittest.TestCase):
    def test_tls_auth_and_exact_file_routes(self):
        with tempfile.TemporaryDirectory() as d:
            state = Path(d)
            feed.init(state, '192.168.1.2', 8845)
            c = json.loads((state / 'config.json').read_text())
            self.assertEqual((state / 'server.key').stat().st_mode & 0o777, 0o600)
            with self.assertRaises(ValueError): feed.init(state, '192.168.1.2', 8845)
            folder = state / 'packages/launcher/test'
            folder.mkdir(parents=True)
            (folder / 'manifest.json').write_bytes(b'only-package-data')
            feed.atomic_json(state / 'current.json', {'launcher':'packages/launcher/test'})
            tls = ssl.SSLContext(ssl.PROTOCOL_TLS_SERVER)
            tls.load_cert_chain(state / 'server.crt', state / 'server.key')
            server = feed.Server(('127.0.0.1', 0), feed.Feed)
            server.state, server.token, server.tls = state, c['access_token'], tls
            thread = threading.Thread(target=server.serve_forever, daemon=True)
            thread.start()
            context = ssl.create_default_context(cafile=str(state / 'server.crt'))
            context.check_hostname = False  # Fixture SAN is LAN IP; TCP is test loopback.
            def request(path, authenticated=True):
                connection = http.client.HTTPSConnection('127.0.0.1', server.server_port, context=context)
                try:
                    connection.request('GET', path, headers={'Authorization':'Bearer '+c['access_token']} if authenticated else {})
                    response = connection.getresponse()
                    return response.status, response.read()
                finally: connection.close()
            try:
                self.assertEqual(request('/launcher/manifest.json', False)[0],401)
                self.assertEqual(request('/launcher/manifest.json'),(200,b'only-package-data'))
                for path in ('/config.json','/server.key','/launcher/../config.json','/launcher/manifest.json?x=1','/','/backend/backend.zip'):
                    self.assertEqual(request(path)[0],404,path)
                (folder / 'manifest.json').unlink()
                (folder / 'manifest.json').symlink_to(state / 'config.json')
                self.assertEqual(request('/launcher/manifest.json')[0],404)
            finally:
                server.shutdown();server.server_close();thread.join()

if __name__ == '__main__':unittest.main()
