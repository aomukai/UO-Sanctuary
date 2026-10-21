"""Exercise real class/craft swaps, stored gear retrieval and the class skill panel."""
import json
import os
from pathlib import Path
import subprocess
import time


def exercise(window, click, capture):
    def command(suffix):
        name = 'full-wardrobe-ui-' + suffix
        log = Path(os.environ['SANCTUARY_TEST_LOG']); offset = log.stat().st_size
        os.write(int(os.environ['SANCTUARY_TEST_CONSOLE_FD']), (name + '\n').encode())
        deadline = time.monotonic() + 12
        while time.monotonic() < deadline:
            text = log.read_text(errors='replace')[offset:]
            if name + ': failed' in text:
                capture('t40-full-wardrobe-failure.png'); raise AssertionError(text[-3000:])
            if name + ': passed' in text:
                return json.loads(Path(os.environ['SANCTUARY_TEST_UI_STATE']).read_text())
            time.sleep(.05)
        raise AssertionError('Missing ' + name)
    def key(value):
        subprocess.run(['xdotool', 'key', '--window', window, value], check=True); time.sleep(.4)
    click(335, 367); command('setup'); time.sleep(.5); click(410, 58)
    capture('t40-wardrobe-all-classes.png')
    click(65, 151); click(338, 303); click(75, 408); command('sorceress')
    key('s'); capture('t40-fresh-class-skills.png'); key('s')
    command('open'); time.sleep(.3); click(338, 443); time.sleep(.4)
    capture('t40-stored-equipment.png'); click(75, 128); command('retrieved')
    click(450, 418); time.sleep(.3); click(65, 176); click(338, 127); click(75, 408)
    command('back'); key('s'); capture('t40-restored-necromancer-skills.png'); key('s')
    command('final')
    print('Actual client: all-class selector and sharing warning, simultaneous class/craft swap, real gear retrieval, parked summons and restored class quick bindings passed.', flush=True)
