"""Exercise both civic ceremonies and a paid active-class respec in ClassicUO."""
import json
import os
from pathlib import Path
import time

def exercise(window, click, capture):
    log = Path(os.environ['SANCTUARY_TEST_LOG'])
    console = int(os.environ['SANCTUARY_TEST_CONSOLE_FD'])
    def command(suffix):
        name = 'recognition-ui-' + suffix
        offset = log.stat().st_size
        os.write(console, (name + '\n').encode())
        deadline = time.monotonic() + 8
        while time.monotonic() < deadline:
            text = log.read_text(errors='replace')[offset:]
            if name + ': failed' in text:
                raise AssertionError(text[-3000:])
            if name + ': passed' in text:
                return json.loads(Path(os.environ['SANCTUARY_TEST_UI_STATE']).read_text())
            time.sleep(.05)
        raise AssertionError('No response for ' + name)
    click(335, 367)
    command('setup')
    time.sleep(.7)
    capture('t14-recognition-hero.png')
    click(350, 140)
    command('first')
    time.sleep(.5)
    capture('t14-recognition-pillar.png')
    click(350, 200)
    command('second')
    time.sleep(.5)
    capture('t14-recognition-both.png')
    click(75, 435)
    command('quote')
    capture('t14-respec-confirm.png')
    command('remote')
    click(80, 345)
    command('rejected')
    time.sleep(.4)
    click(75, 435)
    command('quote')
    click(80, 345)
    command('live-final')
    capture('t14-respec-complete.png')
    print('Actual client: both ceremonies award distinct certificates and one wardrobe; second ceremony unlocks respec; remote response rejects; 5,000-gold active-class reset refunds points and closes the UI.', flush=True)
