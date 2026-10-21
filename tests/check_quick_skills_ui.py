"""Drive S, hover F1/F2/F12, mouse bindings and cursor quick casts in the real client."""
import json
import os
from pathlib import Path
import subprocess
import time


def exercise(window, click, capture):
    def command(suffix):
        name = 'quick-skill-ui-' + suffix
        log = Path(os.environ['SANCTUARY_TEST_LOG']); offset = log.stat().st_size
        os.write(int(os.environ['SANCTUARY_TEST_CONSOLE_FD']), (name + '\n').encode())
        deadline = time.monotonic() + 12
        while time.monotonic() < deadline:
            text = log.read_text(errors='replace')[offset:]
            if name + ': failed' in text:
                capture('t39-quick-skills-failure.png'); raise AssertionError(text[-3000:])
            if name + ': passed' in text:
                return json.loads(Path(os.environ['SANCTUARY_TEST_UI_STATE']).read_text())
            time.sleep(.05)
        raise AssertionError('Missing ' + name)
    def mouse(x, y, button=None):
        subprocess.run(['xdotool', 'mousemove', '--window', window, str(x), str(y)], check=True); time.sleep(.2)
        if button:
            subprocess.run(['xdotool', 'click', str(button)], check=True); time.sleep(.35)
    def key(value):
        subprocess.run(['xdotool', 'key', '--window', window, value], check=True); time.sleep(.3)
    click(335, 367); state = command('setup'); time.sleep(.8)
    key('s'); time.sleep(.3); capture('t39-available-skills.png')
    mouse(180, 269); key('F1'); key('F12'); mouse(180, 307); key('F2')
    mouse(180, 269, 1); mouse(180, 307, 3); state = command('assigned')
    capture('t39-assigned-quick-bar.png'); key('s'); mouse(450, 230); key('Return')
    subprocess.run(['xdotool', 'type', '--window', window, '--delay', '100', 'hello'], check=True); time.sleep(.2)
    key('F1'); command('chat'); key('ctrl+a'); key('BackSpace'); key('Return'); key('F1'); time.sleep(.8); state = command('teeth')
    mouse(570, 80); key('F2'); time.sleep(.3); command('invalid')
    a, b = state['player'], state['corpse']
    point = (310 + 22 * (b['x'] - a['x'] - b['y'] + a['y']), 250 + 22 * (b['x'] - a['x'] + b['y'] - a['y']) - 4 * (b['z'] - a['z']))
    mouse(*point); key('F2'); time.sleep(1); command('raised'); capture('t39-quick-summon.png')
    mouse(450, 230); key('F1'); command('unavailable'); key('s'); time.sleep(.4)
    # Only basic attack and Clear remain after respec.
    mouse(180, 269); key('F12'); command('cleared'); key('s'); command('final')
    capture('t39-quick-skills-final.png')
    print('Actual S panel, hover F1/F2/F12, both mouse bindings, cursor Teeth, hovered-corpse summon, unavailable skills and clearing passed.', flush=True)
