"""Actual native Tinkering menu replies against an isolated fixture world."""
import json
import os
from pathlib import Path
import subprocess
import time

ROOT = Path(__file__).resolve().parents[1]


def command(name):
    log = Path(os.environ.get('SANCTUARY_TEST_LOG', ROOT / '.local/logs/britain-baseline-check.log'))
    offset = log.stat().st_size
    os.write(int(os.environ['SANCTUARY_TEST_CONSOLE_FD']), ('crafting-ui-' + name + '\n').encode())
    deadline = time.monotonic() + 10
    while time.monotonic() < deadline:
        output = log.read_text(errors='replace')[offset:]
        if f'crafting-ui-{name}: failed' in output:
            raise AssertionError(output[-3000:])
        if f'crafting-ui-{name}: passed' in output:
            return json.loads(Path(os.environ['SANCTUARY_TEST_UI_STATE']).read_text())
        time.sleep(.05)
    raise AssertionError(f'Missing crafting-ui-{name}')


def exercise(window, click, capture):
    def double(x, y):
        subprocess.run(['xdotool', 'mousemove', '--window', window, str(x), str(y),
                        'click', '--repeat', '2', '--delay', '100', '1'], check=True)
        time.sleep(.5)

    click(335, 367)
    command('setup')
    time.sleep(.3)
    click(130, 182)  # Production asks for a profession; processing remains open.
    capture('crafting-choice.png')
    click(130, 322)
    command('confirmed')
    click(552, 105)
    command('material')  # Native resource selection; actual menu replies below use the client.
    time.sleep(.5)
    capture('crafting-main.png')
    subprocess.run(['xdotool', 'mousemove', '--window', window, '200', '190', 'click', '3'], check=True)
    time.sleep(.3)
    command('cancelled')
    command('material')
    time.sleep(.3)
    double(150, 155)
    capture('crafting-tools.png')
    double(168, 153)
    time.sleep(4)
    capture('crafting-produced.png')
    command('crafted')
    command('full')
    double(150, 155)
    double(168, 153)
    time.sleep(4)
    capture('crafting-full-pack.png')
    command('rejected')
    subprocess.run(['xdotool', 'mousemove', '--window', window, '200', '190', 'click', '3'], check=True)
    time.sleep(.3)
    command('cauldron')
    time.sleep(.7)
    capture('crafting-cauldron.png')
    command('town')
    time.sleep(.7)
    capture('crafting-town.png')
    subprocess.run(['xdotool', 'windowclose', window], check=True)
    time.sleep(2)
    state = command('disconnected')
    print('Actual client: profession confirmation, native tool recipe, full-pack rejection and disconnected-ticket rejection passed.', state, flush=True)
