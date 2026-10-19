"""Real SDL client receives the native salvage gumps and sends confirm/cancel replies.

The console fixture supplies target selections, isolating the new gump behavior from
existing backpack pixel picking. It invokes the normal server Target callback.
"""
import json
import os
from pathlib import Path
import subprocess
import time

ROOT = Path(__file__).resolve().parents[1]


def command(name):
    log = ROOT / '.local/logs/britain-baseline-check.log'
    offset = log.stat().st_size
    os.write(int(os.environ['SANCTUARY_TEST_CONSOLE_FD']), ('economy-ui-' + name + '\n').encode())
    deadline = time.monotonic() + 6
    while time.monotonic() < deadline:
        output = log.read_text(errors='replace')[offset:]
        if f'economy-ui-{name}: failed' in output:
            raise AssertionError(output[-2500:])
        if f'economy-ui-{name}: passed' in output:
            return json.loads(Path(os.environ['SANCTUARY_TEST_UI_STATE']).read_text())
        time.sleep(.05)
    raise AssertionError(f'Missing economy-ui-{name}')


def exercise(window, click, capture):
    click(335, 367)
    command('setup')

    def open_menu():
        subprocess.run(['xdotool', 'key', '--window', window, 'Return'], check=True)
        subprocess.run(['xdotool', 'type', '--window', window, '--delay', '80', '[Essence'], check=True)
        subprocess.run(['xdotool', 'key', '--window', window, 'Return'], check=True)
        time.sleep(.6)

    open_menu()
    capture('economy-menu.png')
    click(130, 412)  # Native gump at (100,80), salvage button (22,324).
    command('salvage-target')
    time.sleep(.4)
    capture('economy-salvage-confirm.png')
    click(365, 412)  # Cancel the destructive action.
    command('cancelled')
    open_menu()
    click(130, 412)
    command('salvage-target')
    time.sleep(.3)
    click(130, 412)  # Confirm once.
    command('salvaged')
    open_menu()
    click(365, 412)  # Imbue button.
    command('white-target')
    time.sleep(.4)
    capture('economy-imbue-confirm.png')
    click(130, 412)
    # Exercise vendor-origin rejection through the same target callback.
    open_menu()
    click(130, 412)
    command('vendor-target')
    capture('economy-vendor-rejected.png')
    state = command('final')
    print('Actual client: native menu, destructive confirmation/cancel, salvage, imbuing and vendor rejection passed.', state, flush=True)
