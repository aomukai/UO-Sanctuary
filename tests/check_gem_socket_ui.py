"""Real client native mining reward, gem upgrade and permanent rune installation."""
import json
import os
from pathlib import Path
import subprocess
import time


def exercise(window, click, capture):
    log = Path(os.environ['SANCTUARY_TEST_LOG'])
    def command(suffix):
        name = 'gem-socket-ui-' + suffix
        offset = log.stat().st_size
        os.write(int(os.environ['SANCTUARY_TEST_CONSOLE_FD']), (name + '\n').encode())
        end = time.monotonic() + 12
        while time.monotonic() < end:
            output = log.read_text(errors='replace')[offset:]
            if name + ': failed' in output:
                raise AssertionError(output[-4000:])
            if name + ': passed' in output:
                return json.loads(Path(os.environ['SANCTUARY_TEST_UI_STATE']).read_text())
            time.sleep(.05)
        raise AssertionError('Missing response: ' + name)
    click(335, 367)
    command('setup')
    time.sleep(.6)
    capture('t24-t25-tinker-menu.png')
    click(130, 350)
    command('gem-target')
    time.sleep(.6)
    capture('t24-t25-gem-confirm.png')
    click(365, 315)
    command('gem-cancelled')
    click(130, 350)
    command('gem-target')
    click(102, 315)
    time.sleep(5)
    command('gem-crafted')
    # The fixture supplies targets server-side; clear its client cursor before the next real double-click.
    subprocess.run(['xdotool', 'key', '--window', window, 'Escape'], check=True)
    command('rune-setup')
    time.sleep(1.5)
    capture('t24-t25-rune-ready.png')
    command('rune-ready')
    subprocess.run(['xdotool', 'mousemove', '--window', window, '130', '145', 'click', '--repeat', '2', '--delay', '100', '1'], check=True)
    time.sleep(.5)
    capture('t24-t25-rune-clicked.png')
    command('rune-target')
    capture('t24-t25-rune-confirm.png')
    click(365, 370)
    command('rune-cancelled')
    command('rune-target')
    click(102, 370)
    time.sleep(.6)
    command('inserted')
    subprocess.run(['xdotool', 'key', '--window', window, 'Escape'], check=True)
    subprocess.run(['xdotool', 'mousemove', '--window', window, '158', '140'], check=True)
    time.sleep(1.3)
    capture('t24-t25-installed-rune.png')
    command('final')
    print('Actual client: native ore find -> gem upgrade menu/target/cancel/native timer -> rune double-click/target/cancel/permanent install -> level30/FHR10 tooltip, equip/unequip and replay checks. Mining rare ticket seeded in fixture.', flush=True)
