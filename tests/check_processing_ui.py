"""Actual client processing gump responses; material hand-in uses the native target callback."""
import json
import os
from pathlib import Path
import time

ROOT = Path(__file__).resolve().parents[1]


def command(name):
    log = ROOT / '.local/logs/britain-baseline-check.log'
    offset = log.stat().st_size
    os.write(int(os.environ['SANCTUARY_TEST_CONSOLE_FD']), ('processing-ui-' + name + '\n').encode())
    deadline = time.monotonic() + 10
    while time.monotonic() < deadline:
        output = log.read_text(errors='replace')[offset:]
        if f'processing-ui-{name}: failed' in output:
            raise AssertionError(output[-3000:])
        if f'processing-ui-{name}: passed' in output:
            return json.loads(Path(os.environ['SANCTUARY_TEST_UI_STATE']).read_text())
        time.sleep(.05)
    raise AssertionError(f'Missing processing-ui-{name}')


def exercise(window, click, capture):
    import subprocess
    click(335, 367)
    command('setup')
    time.sleep(.4)
    capture('processing-tool.png')

    def target():
        click(130, 357)
        command('target')  # Native material hand-in after the actual tool-button reply.
        time.sleep(.3)

    target()
    capture('processing-warning.png')
    click(130, 367)
    command('cancelled')
    command('tool')
    target()
    click(130, 322)
    command('partial')
    command('full')
    target()
    click(130, 322)
    command('rejected')
    capture('processing-full-pack.png')
    command('relief')
    target()
    click(385, 322)
    command('completed')
    capture('processing-completed.png')
    subprocess.run(['xdotool', 'windowclose', window], check=True)
    time.sleep(2)
    state = command('disconnected')
    print('Actual client: processing without craft selection, warning/cancel, partial quantity, full-pack rejection, encumbered make-all and disconnected request passed.', state, flush=True)
