"""Actual scroll right-click/cancel, belt world cursor and learned Teleport."""
import json
import os
from pathlib import Path
import subprocess
import time


def exercise(window, click, capture):
    def command(suffix):
        name = 'teleport-ui-' + suffix
        log = Path(os.environ['SANCTUARY_TEST_LOG'])
        offset = log.stat().st_size
        os.write(int(os.environ['SANCTUARY_TEST_CONSOLE_FD']), (name + '\n').encode())
        deadline = time.monotonic() + 10
        while time.monotonic() < deadline:
            text = log.read_text(errors='replace')[offset:]
            if name + ': failed' in text:
                raise AssertionError(text[-3000:])
            if name + ': passed' in text:
                return json.loads(Path(os.environ['SANCTUARY_TEST_UI_STATE']).read_text())
            time.sleep(.05)
        raise AssertionError('No response for ' + name)

    def mouse(x, y, button=None):
        subprocess.run(['xdotool', 'mousemove', '--window', window, str(x), str(y)], check=True)
        time.sleep(.15)
        if button:
            subprocess.run(['xdotool', 'click', str(button)], check=True)
            time.sleep(.3)

    def key(k):
        subprocess.run(['xdotool', 'key', '--window', window, k], check=True)
        time.sleep(.2)

    click(335, 367)
    command('setup')
    time.sleep(.5)
    capture('t33-scrolls.png')
    mouse(122, 132, 3)
    command('target')
    capture('t33-inventory-target.png')
    key('Escape')
    command('cancelled')
    time.sleep(1.2)
    mouse(122, 132, 3)
    capture('t33-retarget.png')
    command('target')
    click(400, 250)
    time.sleep(.8)
    command('cast')
    click(458, 439)
    click(162, 132)
    command('loaded')
    capture('t33-belt-scroll.png')
    mouse(460, 440)
    key('1')
    command('invalid')
    mouse(400, 250)
    key('1')
    time.sleep(.8)
    command('belt')
    mouse(350, 280, 3)
    time.sleep(.8)
    command('final')
    capture('t33-learned-teleport.png')
    print('Actual inventory right-click/target/cancel, belt load, invalid UI cursor, direct world aim and learned Teleport passed.', flush=True)
