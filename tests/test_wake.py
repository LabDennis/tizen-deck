import sys, tempfile, unittest
from pathlib import Path
from unittest.mock import patch, Mock
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from wake import WakeOnLan, validate


class Tests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.wake = WakeOnLan(Path(self.temp.name))

    def test_configuration_validation_and_persistence(self):
        self.assertEqual(self.wake.config()['mac'], '')
        saved = self.wake.save('02:00:00:00:ab:cd', '192.0.2.255')
        self.assertEqual(WakeOnLan(Path(self.temp.name)).config(), saved)
        for mac in ('', '00:00:00:00:00:00', 'ff:ff:ff:ff:ff:ff', '02:00:00:00:00:00; id'):
            with self.assertRaises(ValueError): validate(mac, '192.0.2.255')
        for target in (True, 'localhost', '127.0.0.1', '224.0.0.1'):
            with self.assertRaises(ValueError): validate('02:00:00:00:ab:cd', target)

    @patch('wake.socket.socket')
    @patch('wake.time.sleep')
    def test_magic_packet_is_fixed_and_sending_is_not_confirmation(self, sleep, socket):
        self.wake.save('02:00:00:00:ab:cd', '192.0.2.255')
        result = self.wake.wake(Mock(side_effect=OSError('asleep')), timeout=0)
        sock = socket.return_value.__enter__.return_value
        expected = b'\xff' * 6 + bytes.fromhex('02000000abcd') * 16
        self.assertEqual(sock.sendto.call_count, 3)
        sock.sendto.assert_called_with(expected, ('192.0.2.255', 9))
        self.assertTrue(result['sent']); self.assertFalse(result['awake'])

    @patch('wake.socket.socket')
    def test_already_on_does_not_send_or_toggle(self, socket):
        self.wake.save('02:00:00:00:ab:cd', '192.0.2.255')
        result = self.wake.wake(lambda: {'PowerState': 'on'})
        socket.assert_not_called()
        self.assertTrue(result['awake']); self.assertFalse(result['sent'])

    @patch('wake.socket.socket')
    @patch('wake.time.sleep')
    def test_power_state_is_required_for_wake_confirmation(self, sleep, socket):
        self.wake.save('02:00:00:00:ab:cd', '192.0.2.255')
        result = self.wake.wake(Mock(side_effect=[{'PowerState':'standby'}, {'PowerState':'on'}]))
        self.assertTrue(result['awake']); self.assertTrue(result['sent'])

if __name__ == '__main__': unittest.main()
