"""Exercise free corresponding-tool salvage in the actual ClassicUO client."""
import json
import os
from pathlib import Path
import time

def exercise(window, click, capture):
    log = Path(os.environ['SANCTUARY_TEST_LOG'])
    console = int(os.environ['SANCTUARY_TEST_CONSOLE_FD'])
    def command(suffix):
        name = 'salvage-ui-' + suffix
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
    def salvage():
        time.sleep(.25)
        click(130, 400)
    click(335, 367)
    command('setup')
    time.sleep(.4)
    capture('t16-salvage-tool.png')
    salvage()
    command('target')
    time.sleep(.3)
    capture('t16-salvage-confirm.png')
    click(365, 412)
    command('cancelled')
    salvage()
    command('wrong-target')
    salvage()
    command('vendor-target')
    salvage()
    command('target')
    command('remove-tool')
    click(130, 412)
    command('rejected')
    salvage()
    command('target')
    click(130, 412)
    command('live-final')
    capture('t16-salvage-complete.png')
    print('Actual client: production-tool salvage outside a town facility with zero matching skill and another active craft; confirmation/cancel, wrong tool/vendor/missing tool rejection, destructive contents and zero uses/gold/skill gain passed.', flush=True)
