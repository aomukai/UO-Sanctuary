"""Exercise actual first-choice and practice gump responses in a disposable world."""
import json
import os
from pathlib import Path
import subprocess
import time

ROOT = Path(__file__).resolve().parents[1]


def command(name):
    log = ROOT / '.local/logs/britain-baseline-check.log'
    offset = log.stat().st_size
    os.write(int(os.environ['SANCTUARY_TEST_CONSOLE_FD']), ('profession-ui-' + name + '\n').encode())
    deadline = time.monotonic() + 8
    while time.monotonic() < deadline:
        output = log.read_text(errors='replace')[offset:]
        if f'profession-ui-{name}: failed' in output:
            raise AssertionError(output[-2500:])
        if f'profession-ui-{name}: passed' in output:
            return json.loads(Path(os.environ['SANCTUARY_TEST_UI_STATE']).read_text())
        time.sleep(.05)
    raise AssertionError(f'Missing profession-ui-{name}')


def exercise(window, click, capture):
    click(335, 367)
    command('setup')
    time.sleep(.3)
    capture('profession-default.png')
    click(552, 105)  # Close status before opening the tool.
    command('tool')
    time.sleep(.3)
    click(130, 182)
    capture('profession-first-choice.png')
    click(400, 322)
    command('cancelled')
    command('trainer')
    time.sleep(.3)
    capture('profession-trainer-choice.png')
    click(130, 322)
    command('confirmed')
    command('pay')  # Native gold hand-in; first-choice confirmation above used the client.
    command('tool')
    time.sleep(.3)
    capture('profession-practice.png')
    click(130, 292)
    state = command('final')
    command('status')
    time.sleep(.3)
    capture('profession-trained.png')
    print('Actual client: initial Alchemy/open choice, cancel, trainer first-choice confirmation and practice response passed.', state, flush=True)
