"""Actual client mortar selection, cancellation, native brew and restorative belt keys."""
import json
import os
from pathlib import Path
import subprocess
import time


def exercise(window, click, capture):
    log = Path(os.environ['SANCTUARY_TEST_LOG'])
    console = int(os.environ['SANCTUARY_TEST_CONSOLE_FD'])

    def command(suffix):
        name = 'alchemy-ui-' + suffix
        offset = log.stat().st_size
        os.write(console, (name + '\n').encode())
        deadline = time.monotonic() + 10
        while time.monotonic() < deadline:
            text = log.read_text(errors='replace')[offset:]
            if name + ': failed' in text:
                raise AssertionError(text[-3500:])
            if name + ': passed' in text:
                return json.loads(Path(os.environ['SANCTUARY_TEST_UI_STATE']).read_text())
            time.sleep(.05)
        raise AssertionError('No response for ' + name)

    click(335, 367)
    command('setup')
    time.sleep(.4)
    click(130, 322)
    time.sleep(.4)
    capture('t28-restorative-recipes.png')
    click(90, 283)
    command('quoted')
    capture('t28-brew-confirm.png')
    click(605, 72)
    command('cancelled')
    click(130, 322)
    click(90, 283)
    command('quoted')
    click(90, 378)
    time.sleep(4)
    command('crafted')
    subprocess.run(['xdotool', 'mousemove', '--window', window, '460', '440'], check=True)
    time.sleep(.8)
    capture('t28-crafted-belt-mixture.png')
    subprocess.run(['xdotool', 'key', '--window', window, '1'], check=True)
    time.sleep(.2)
    command('drunk')
    time.sleep(.3)
    subprocess.run(['xdotool', 'key', '--window', window, '1'], check=True)
    time.sleep(.2)
    command('final')
    capture('t28-restored.png')
    print('Actual client: mortar recipes/confirmation/cancel/native brew, exact costs, crafted rejuvenation and dropped Super belt use, unchanged potion buff/action/recovery passed.', flush=True)
