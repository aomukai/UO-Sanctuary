"""Actual combat-buff brewing and native client/server running cadence."""
import json
import os
from pathlib import Path
import statistics
import subprocess
import time


def exercise(window, click, capture):
    log = Path(os.environ['SANCTUARY_TEST_LOG'])
    console = int(os.environ['SANCTUARY_TEST_CONSOLE_FD'])

    def command(suffix):
        name = 'combat-potion-ui-' + suffix
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
    click(455, 72)
    capture('t29-combat-recipes.png')
    click(90, 133)
    command('quoted')
    capture('t29-combat-confirm.png')
    click(605, 72)
    command('cancelled')
    click(130, 322)
    click(455, 72)
    click(90, 133)
    command('quoted')
    click(90, 378)
    time.sleep(4)
    command('crafted')

    def route(phase):
        state = command(phase)
        time.sleep(.7)
        a, b = state['player'], state['goal']
        x = 310 + 22 * (b['x'] - a['x'] - b['y'] + a['y'])
        y = 250 + 22 * (b['x'] - a['x'] + b['y'] - a['y']) - 4 * (b['z'] - a['z'])
        subprocess.run(['xdotool', 'mousemove', '--window', window, str(x), str(y)], check=True)
        time.sleep(.2)
        subprocess.run(['xdotool', 'click', '1'], check=True)
        time.sleep(2.5)
        times = command('checked')['steps']
        return statistics.median(b - a for a, b in zip(times, times[1:]))

    baseline = route('baseline')
    subprocess.run(['xdotool', 'mousemove', '--window', window, '460', '440'], check=True)
    time.sleep(.8)
    capture('t29-second-wind-belt.png')
    subprocess.run(['xdotool', 'key', '--window', window, '1'], check=True)
    time.sleep(.2)
    boosted = route('boosted')
    assert 190 <= baseline <= 240, baseline
    assert 145 <= boosted <= 185 and boosted < baseline * .9, (baseline, boosted)
    command('expire')
    time.sleep(1.5)
    expired = route('expired')
    assert 190 <= expired <= 240, expired
    command('final')
    capture('t29-second-wind-expired.png')
    print(f'Actual combat brew/cancel/belt passed; running cadence {baseline:.0f}ms -> {boosted:.0f}ms -> {expired:.0f}ms after expiry.', flush=True)
