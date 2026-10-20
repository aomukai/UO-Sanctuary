"""Real client menus and confirmation replies; fixtures supply native item targets."""
import json
import os
from pathlib import Path
import time

def exercise(window, click, capture):
    log = Path(os.environ['SANCTUARY_TEST_LOG'])
    console = int(os.environ['SANCTUARY_TEST_CONSOLE_FD'])
    def command(suffix):
        name = 'imbuing-ui-' + suffix
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
    time.sleep(.3)
    capture('t17-jewellery-grades.png')
    click(65, 122)
    command('material')
    time.sleep(4)
    command('crafted')
    time.sleep(.3)
    click(130, 183)
    time.sleep(.3)
    click(365, 412)
    command('imbue-target')
    time.sleep(.3)
    capture('t17-imbue-confirm.png')
    click(365, 412)
    command('cancelled')
    time.sleep(.3)
    click(130, 183)
    time.sleep(.3)
    click(365, 412)
    command('imbue-target')
    click(130, 412)
    command('imbued')
    time.sleep(.3)
    capture('t18-scribe-tool.png')
    click(130, 322)
    time.sleep(4)
    command('scribed')
    capture('t18-identify-target.png')
    command('invalid')
    command('identify')
    command('live-final')
    capture('t18-identified.png')
    print('Actual client: jewellery grade menu and craft, Imbuing confirm/cancel/payment, Identify scroll crafting and native valid/invalid targets passed.', flush=True)
