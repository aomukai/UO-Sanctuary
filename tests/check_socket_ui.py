"""Actual ClassicUO socket menu, warning, cancellation and intuition choices.
The console supplies item targets and seeds the rare initial intuition only;
attempt, retry and abort are actual client button clicks.
"""
import json
import os
from pathlib import Path
import time

def exercise(window, click, capture):
    log = Path(os.environ['SANCTUARY_TEST_LOG'])
    console = int(os.environ['SANCTUARY_TEST_CONSOLE_FD'])
    def command(suffix):
        name = 'socket-ui-' + suffix
        offset = log.stat().st_size
        os.write(console, (name + '\n').encode())
        deadline = time.monotonic() + 8
        while time.monotonic() < deadline:
            text = log.read_text(errors='replace')[offset:]
            if name + ': failed' in text:
                raise AssertionError(text[-3000:])
            if name + ': passed' in text:
                return json.loads(Path(os.environ['SANCTUARY_TEST_UI_STATE']).read_text())
            time.sleep(.05)
        raise AssertionError('No response for ' + name)
    def target():
        time.sleep(1)
        click(130, 298)
        command('target')
        time.sleep(1)
    click(335, 367)
    command('setup')
    time.sleep(1.5)
    capture('t20-socket-tool.png')
    target()
    capture('t20-socket-white-warning.png')
    click(363, 408)
    command('cancelled')
    target()
    click(100, 408)
    command('added')
    time.sleep(1.5)
    capture('t20-socket-intuition.png')
    click(363, 408)
    command('aborted')
    time.sleep(1.5)
    click(100, 408)
    command('live-final')
    capture('t20-socket-complete.png')
    print('Actual client: tool menu, white-item warning, safe cancel, first socket, free intuition abort and one paid terminal retry passed. Targets and rare initial intuition are server fixtures.', flush=True)
