"""Actual native craft timer, recipe pages, cancellation and keg transfers."""
import json
import os
from pathlib import Path
import subprocess
import time


def exercise(window, click, capture):
    log = Path(os.environ['SANCTUARY_TEST_LOG'])
    console = int(os.environ['SANCTUARY_TEST_CONSOLE_FD'])

    def command(suffix):
        name = 'carpentry-ui-' + suffix
        offset = log.stat().st_size
        os.write(console, (name + '\n').encode())
        deadline = time.monotonic() + 12
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
    click(130, 322)
    capture('t31-carpentry-parts.png')
    click(425, 120)  # Stations.
    capture('t31-carpentry-stations.png')
    click(485, 430)  # Next station page.
    capture('t31-carpentry-stations-page2.png')
    click(250, 90)  # Seating.
    click(75, 285)  # Wooden chair, fourth row.
    command('quoted')
    capture('t31-carpentry-confirm.png')
    click(600, 55)
    command('cancelled')
    click(130, 322)
    click(250, 90)
    click(75, 285)
    click(75, 450)
    time.sleep(4)
    command('crafted')
    capture('t31-keg-menu.png')
    click(115, 205)  # Pour target.
    click(122, 132)  # Potion in the backpack.
    command('poured')
    capture('t31-keg-filled.png')
    click(115, 245)  # Fill one empty bottle.
    command('withdrawn')
    capture('t31-keg-withdrawn.png')
    command('final')
    print('Actual 66-recipe book, station pages, cancel, native timed chair crafting and exact custom keg transfer passed.', flush=True)
