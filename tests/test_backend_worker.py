import json
import os
from pathlib import Path
import subprocess
import tempfile
import unittest

SOURCE = Path(__file__).parents[1] / 'tv-service/backend-update.sh'

class WorkerTest(unittest.TestCase):
    def run_worker(self, fail=False, wrong_version=False):
        with tempfile.TemporaryDirectory(prefix='deck-worker-') as td:
            base = Path(td)
            root, depot, units, bins = (base / x for x in ('root', 'depot', 'units', 'bin'))
            next_dir, backup, job = (depot / x for x in ('next', 'backup', 'job'))
            for folder in (root / 'state', root / 'updates', next_dir, job, units, bins):
                folder.mkdir(parents=True, exist_ok=True)
            (root / 'state/auth.json').write_text('private-old')
            (root / 'state/control-token').write_text('private-token')
            (root / 'updates/keep').write_text('signed launcher')
            (root / 'DeckTV.dll').write_text('old-good')
            (root / 'deck-tv.service').write_text('good-unit')
            (next_dir / 'DeckTV.dll').write_text('wrong-version' if wrong_version else 'bad' if fail else 'new-good')
            (next_dir / 'deck-tv.service').write_text('new-unit')
            (job / 'previous.json').write_text('{"previous":"old"}')
            (bins / 'systemctl').write_text('#!/bin/sh\nif [ "$1" = stop ] && [ -f "'+str(root / 'state/auth.json')+'" ]; then printf private-latest > "'+str(root / 'state/auth.json')+'"; fi\nexit 0\n')
            (bins / 'curl').write_text('''#!/bin/sh
case "$(cat ROOT_DLL)" in
bad) exit 7;;
wrong-version) echo '{"ready":true,"version":"9.9.9"}';;
old-good) echo '{"ready":true,"version":"1.7.2"}';;
*) echo '{"ready":true,"version":"1.8.0"}';;
esac
'''.replace('ROOT_DLL', str(root / 'DeckTV.dll')))
            (bins / 'sleep').write_text('#!/bin/sh\nexit 0\n')
            for p in bins.iterdir(): p.chmod(0o700)
            script = SOURCE.read_text().replace('/opt/usr/tizen-deck-updates', str(depot)).replace('/opt/usr/tizen-deck', str(root)).replace('/run/systemd/system', str(units)).replace('/usr/bin/dotnet', str(bins / 'dotnet'))
            script = script.replace('NEXT_PATH', str(next_dir)).replace('BACKUP_PATH', str(backup)).replace('JOB_PATH', str(job)).replace('NEW_VERSION', '1.8.0').replace('OLD_VERSION', '1.7.2')
            result = subprocess.run(['/bin/sh'], input=script, text=True, env={**os.environ, 'PATH':str(bins)+':'+os.environ['PATH']}, capture_output=True)
            state = json.loads((depot / 'result.json').read_text())
            fail = fail or wrong_version
            self.assertEqual(state['phase'], 'rolled_back' if fail else 'complete', result.stderr)
            self.assertEqual(result.returncode == 0, not fail)
            self.assertEqual((root / 'state/auth.json').read_text(), 'private-latest')
            self.assertEqual((root / 'updates/keep').read_text(), 'signed launcher')
            self.assertEqual((root / 'DeckTV.dll').read_text(), 'old-good' if fail else 'new-good')
            if not fail: self.assertEqual((backup / 'DeckTV.dll').read_text(), 'old-good')
    def test_wrong_version_restores_previous_service(self): self.run_worker(wrong_version=True)
    def test_success_preserves_latest_state(self): self.run_worker()
    def test_failed_health_restores_previous_service(self): self.run_worker(fail=True)

if __name__ == '__main__': unittest.main()
