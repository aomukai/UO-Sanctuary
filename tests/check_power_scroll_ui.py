"""Use the actual native Power Scroll confirmation, including cancel and skipped steps."""
import json
import os
from pathlib import Path
import time


def exercise(window, click, capture):
    log = Path(os.environ['SANCTUARY_TEST_LOG'])
    def command(suffix):
        name = 'power-scroll-ui-' + suffix
        offset = log.stat().st_size
        os.write(int(os.environ['SANCTUARY_TEST_CONSOLE_FD']), (name + '\n').encode())
        end = time.monotonic() + 15
        while time.monotonic() < end:
            text = log.read_text(errors='replace')[offset:]
            if name + ': failed' in text:
                capture('t38-ui-failure.png')
                raise AssertionError(text[-3000:])
            if name + ': passed' in text:
                return json.loads(Path(os.environ['SANCTUARY_TEST_UI_STATE']).read_text())
            time.sleep(.05)
        raise AssertionError('Missing ' + name)
    click(335,367); command('setup'); time.sleep(.6); capture('t38-power-scroll-confirm.png')
    click(438,373); command('cancelled'); time.sleep(.3)
    click(90,373); command('raised'); time.sleep(.6); capture('t38-native-scroll-guard.png')
    click(133,230); command('guarded'); time.sleep(.3)
    click(90,373); command('final'); time.sleep(.5); capture('t38-cap-raised.png')
    print('Actual boss scroll, cancellation, inactive skill cap use, duplicate/skipped-step retention and native confirmation guard passed.', flush=True)
