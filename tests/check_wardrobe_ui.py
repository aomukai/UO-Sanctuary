"""Exercise public craft switching and second-ceremony respec in ClassicUO."""
import json
import os
from pathlib import Path
import time

def exercise(window, click, capture):
    log = Path(os.environ['SANCTUARY_TEST_LOG'])
    console = int(os.environ['SANCTUARY_TEST_CONSOLE_FD'])
    def command(suffix):
        name = 'wardrobe-ui-' + suffix
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
    command('guards')
    time.sleep(.5)
    capture('t15-wardrobe-initial.png')
    click(75, 408)
    command('unchanged')
    click(338, 303)
    command('remote')
    click(75, 408)
    command('rejected')
    time.sleep(.3)
    click(338, 303)
    click(75, 408)
    command('switched')
    time.sleep(.3)
    capture('t15-wardrobe-tinkering.png')
    click(338, 127)
    click(75, 408)
    command('back')
    time.sleep(.3)
    click(350, 200)
    command('both')
    time.sleep(.3)
    capture('t15-wardrobe-both.png')
    click(75, 445)
    command('quote')
    capture('t15-wardrobe-respec.png')
    click(80, 345)
    command('live-final')
    capture('t15-wardrobe-complete.png')
    print('Actual client: preselected class/craft; malformed and stale selections reject; unchanged and moved-away responses are safe; free craft roundtrip preserves skills, points, pools and gear; second ceremony respec charges once and leaves UI.', flush=True)
