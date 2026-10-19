"""A real RMB Teeth cast interrupts a moving zombie and displays monster get-hit."""
import json
import os
from pathlib import Path
import subprocess
import time

ROOT = Path(__file__).resolve().parents[1]


def exercise(window, click, capture):
    log = ROOT / '.local/logs/britain-baseline-check.log'

    def command(suffix):
        name = 'creature-recovery-ui-' + suffix
        offset = log.stat().st_size
        os.write(int(os.environ['SANCTUARY_TEST_CONSOLE_FD']), (name + '\n').encode())
        end = time.monotonic() + 6
        while time.monotonic() < end:
            output = log.read_text(errors='replace')[offset:]
            if name + ': failed' in output:
                raise AssertionError(output[-2300:])
            if name + ': passed' in output:
                return json.loads(Path(os.environ['SANCTUARY_TEST_UI_STATE']).read_text())
            time.sleep(.02)
        raise AssertionError('Missing ' + name)

    try:
        command('setup')
        time.sleep(.7)
        subprocess.run(['xdotool', 'mousemove', '--window', window, '398', '281'], check=True)
        time.sleep(.2)
        subprocess.run(['xdotool', 'click', '3'], check=True)
        end = time.monotonic() + 3
        while True:
            state = command('state')
            if state['seen']:
                break
            assert time.monotonic() < end, {k: v for k, v in state.items() if k != 'samples'}
            time.sleep(.025)
        command('locked')
        capture('creature-hit-recovery.png')
        time.sleep(2)
        state = command('resumed')
        (ROOT / '.local/logs/creature-hit-recovery-ui.json').write_text(json.dumps(state, indent=2))
        print('Creature recovery: real RMB Teeth caused 680ms zombie recovery; approach resumed without movement during recovery.', flush=True)
    finally:
        command('final')
