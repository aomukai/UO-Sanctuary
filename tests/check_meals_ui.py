"""Actual cooking book, native craft timer, inventory meal use and light pixels."""
import json
import os
from pathlib import Path
import subprocess
import time
from PIL import ImageGrab, ImageStat


def exercise(window, click, capture):
    log = Path(os.environ['SANCTUARY_TEST_LOG'])
    console = int(os.environ['SANCTUARY_TEST_CONSOLE_FD'])

    def command(suffix):
        name = 'meal-ui-' + suffix
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
    time.sleep(.4)
    click(130, 322)
    time.sleep(.3)
    capture('t30-meals-tier1.png')
    click(423, 87)
    time.sleep(.3)
    capture('t30-meals-tier3.png')
    click(73, 87)
    click(75, 177)
    command('quoted')
    capture('t30-meal-confirm.png')
    click(600, 55)
    command('cancelled')
    click(130, 322)
    click(75, 177)
    command('quoted')
    click(75, 410)
    time.sleep(4)
    command('crafted')
    time.sleep(.8)
    capture('t30-cooked-inventory.png')
    subprocess.run(['xdotool', 'mousemove', '--window', window, '122', '132', 'click', '--repeat', '2', '--delay', '100', '1'], check=True)
    time.sleep(.3)
    capture('t30-inventory-clicked.png')
    command('eaten')
    time.sleep(1.6)
    command('expired')
    # Close the backpack before measuring the world light around the character.
    subprocess.run(['xdotool', 'mousemove', '--window', window, '180', '95', 'click', '3'], check=True)
    time.sleep(1.2)
    geometry = dict(row.split('=', 1) for row in subprocess.check_output(['xdotool', 'getwindowgeometry', '--shell', window], text=True).splitlines())
    x, y = int(geometry['X']), int(geometry['Y'])

    def brightness():
        return sum(ImageStat.Stat(ImageGrab.grab().crop((x+255, y+195, x+365, y+305))).mean[:3])/3

    dark = brightness()
    capture('t30-light-before.png')
    command('light')
    time.sleep(1.2)
    bright = brightness()
    capture('t30-light-radius3.png')
    assert bright > dark + 5, (dark, bright)
    command('light-expire')
    time.sleep(1.7)
    expired = brightness()
    assert abs(expired-dark) < 10, (dark, bright, expired)
    command('final')
    capture('t30-light-expired.png')
    print(f'Actual cooking tiers/cancel/confirm/native brew, 1/20 flour, food at full hunger, independent potion and encumbrance/no spill passed; light brightness {dark:.1f} -> {bright:.1f} -> {expired:.1f}.', flush=True)
