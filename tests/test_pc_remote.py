import sys
import tempfile
import unittest
from pathlib import Path
from unittest.mock import Mock, patch
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from pc_remote import Remote, server_url, decode_key, main


class RemoteTests(unittest.TestCase):
    def test_url_protects_credentials(self):
        for url in ('http://192.0.2.10:8843', 'https://user:secret@example.test',
                    'file:///etc/passwd', 'https://example.test/api', 'https://example.test?q=x'):
            with self.assertRaises(ValueError): server_url(url)
        self.assertEqual(server_url('http://localhost:8765/'), 'http://localhost:8765')
        self.assertEqual(server_url('https://192.0.2.10:8843'), 'https://192.0.2.10:8843')

    def test_keyboard(self):
        for seq, expected in [('\x1b[A', 'up'), ('\x1bOB', 'down'), ('\xe0K', 'left'),
                              ('\x1b', 'back'), (' ', 'ok'), ('\r', 'ok'), ('q', 'quit'), ('x', None)]:
            stream = iter(seq[1:])
            self.assertEqual(decode_key(seq[0], lambda: next(stream, None)), expected)

    def test_native_capabilities(self):
        r = Remote('http://localhost:8765')
        r.session = {'mode': 'tv', 'text_input': False}
        r.request = Mock()
        with self.assertRaisesRegex(ValueError, 'Aufwecken'): r.wake()
        with self.assertRaisesRegex(ValueError, 'Freitexteingabe'): r.text('hello')
        r.request.assert_not_called()

    def test_capture_polls_and_validates_file(self):
        r = Remote('http://localhost:8765')
        png = b'\x89PNG\r\n\x1a\nimage'
        r.request = Mock(side_effect=[{'job': 'test'}, {'status': 'running'},
                                     {'status': 'done', 'result': {}}, png])
        with tempfile.TemporaryDirectory() as d, patch('pc_remote.time.sleep'):
            p = Path(d) / 'screen.png'
            r.capture(p)
            self.assertEqual(p.read_bytes(), png)
            r.request.reset_mock()
            with self.assertRaisesRegex(ValueError, 'existiert'): r.capture(p)
            r.request.assert_not_called()
            p.unlink()
            r.request.side_effect = [{'job': 'test'}, {'status': 'error', 'error': 'device offline'}]
            with self.assertRaisesRegex(ValueError, 'device offline'): r.capture(p)
            self.assertFalse(p.exists())

    def test_main_cleans_up_even_on_failure(self):
        with patch('pc_remote.Remote') as constructor, patch('pc_remote.getpass.getpass', return_value='private'), patch('sys.stderr'):
            remote = constructor.return_value
            remote.key.side_effect = ValueError('offline')
            self.assertEqual(main(['key', 'ok']), 1)
            remote.login.assert_called_once_with('admin', 'private')
            remote.logout.assert_called_once()
