"""Real ClassicUO pen menu, mould target, cancel and native rune craft timer."""
import json
import os
from pathlib import Path
import subprocess
import time


def exercise(window, click, capture):
    log = Path(os.environ['SANCTUARY_TEST_LOG'])
    def command(suffix):
        name = 'rune-ui-' + suffix
        offset = log.stat().st_size
        os.write(int(os.environ['SANCTUARY_TEST_CONSOLE_FD']), (name + '\n').encode())
        deadline = time.monotonic() + 12
        while time.monotonic() < deadline:
            text = log.read_text(errors='replace')[offset:]
            if name + ': failed' in text:
                raise AssertionError(text[-4000:])
            if name + ': passed' in text:
                return json.loads(Path(os.environ['SANCTUARY_TEST_UI_STATE']).read_text())
            time.sleep(.05)
        raise AssertionError('No response for ' + name)
    click(335, 367)
    command('setup')
    time.sleep(.5)
    capture('t23-pen-menu.png')
    click(130, 360)
    command('target')
    time.sleep(.5)
    capture('t23-rune-confirm.png')
    click(365, 423)
    command('cancelled')
    click(130, 360)
    command('target')
    command('seed')
    click(102, 423)
    time.sleep(5)
    command('crafted')
    time.sleep(.6)
    capture('t23-rune-crafted.png')
    subprocess.run(['xdotool', 'mousemove', '--window', window, '133', '140'], check=True)
    time.sleep(1.4)
    capture('t23-rune-tooltip.png')
    command('final')
    print('Actual client: pen rune action -> mould target -> cancel without costs -> confirmation -> native timer -> Superior T8 19% rune, exact costs and no replay. Random success/quality tickets seeded only by fixture.', flush=True)
