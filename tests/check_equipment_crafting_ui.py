"""Actual tier/recipe replies; fixture hands ingredients to native targeting and exercises native equip."""
import json
import os
from pathlib import Path
import subprocess
import time

ROOT = Path(__file__).resolve().parents[1]


def command(name):
    log = Path(os.environ.get('SANCTUARY_TEST_LOG', ROOT / '.local/logs/britain-baseline-check.log'))
    offset = log.stat().st_size
    os.write(int(os.environ['SANCTUARY_TEST_CONSOLE_FD']), ('equipment-crafting-ui-' + name + '\n').encode())
    deadline = time.monotonic() + 10
    while time.monotonic() < deadline:
        output = log.read_text(errors='replace')[offset:]
        if f'equipment-crafting-ui-{name}: failed' in output:
            raise AssertionError(output[-3000:])
        if f'equipment-crafting-ui-{name}: passed' in output:
            return json.loads(Path(os.environ['SANCTUARY_TEST_UI_STATE']).read_text())
        time.sleep(.05)
    raise AssertionError(f'Missing equipment-crafting-ui-{name}')


def exercise(window, click, capture):
    click(335, 367)
    command('setup')
    time.sleep(.4)
    click(130, 322)
    time.sleep(.4)
    capture('equipment-recipes-normal.png')

    def elite():
        click(449, 142)
        time.sleep(.3)
        capture('equipment-recipes-elite.png')
        click(130, 257)
        command('target')
        time.sleep(4)

    elite()
    command('crafted')
    capture('equipment-crafted.png')
    command('full')
    elite()
    command('rejected')
    capture('equipment-full-pack.png')
    command('equip')
    time.sleep(.6)
    click(150, 14)
    time.sleep(.6)
    capture('equipment-equipped.png')
    subprocess.run(['xdotool', 'mousemove', '--window', window, '113', '181'], check=True)
    time.sleep(1.2)
    capture('equipment-tooltip.png')
    command('pagination')
    time.sleep(.4)
    click(530, 402)
    time.sleep(.3)
    capture('equipment-recipes-page2.png')
    click(130, 402)
    time.sleep(.3)
    capture('equipment-recipes-page1.png')
    click(530, 402)
    time.sleep(.3)
    click(130, 192)
    command('page-target')
    time.sleep(4)
    command('page-crafted')
    capture('equipment-page-crafted.png')
    subprocess.run(['xdotool', 'windowclose', window], check=True)
    time.sleep(2)
    state = command('disconnected')
    print('Actual client: tier tabs, Elite Leather Cap recipe, exact costs, full-pack rejection, native level/equip checks, next/previous-page crafting and disconnected-ticket rejection passed.', state, flush=True)
