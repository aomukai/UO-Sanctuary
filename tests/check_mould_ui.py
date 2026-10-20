"""Mouse kill and declared mould pickup in the actual ClassicUO client."""
import json
import os
from pathlib import Path
import statistics
import subprocess
import time
from PIL import ImageGrab

def exercise(window, click, capture):
    log = Path(os.environ['SANCTUARY_TEST_LOG'])
    console = int(os.environ['SANCTUARY_TEST_CONSOLE_FD'])
    def command(suffix):
        name = 'mould-ui-' + suffix
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
    state = command('setup')
    time.sleep(1.5)
    geometry = dict(line.split('=', 1) for line in subprocess.check_output(
        ['xdotool', 'getwindowgeometry', '--shell', window], text=True).splitlines() if '=' in line)
    ox, oy = int(geometry['X']), int(geometry['Y'])
    a, b = state['player'], state['enemy']
    x = 310 + 22 * (b['x'] - a['x'] - b['y'] + a['y'])
    y = 250 + 22 * (b['x'] - a['x'] + b['y'] - a['y']) - 4 * (b['z'] - a['z']) - 60
    image = ImageGrab.grab(xdisplay=os.environ['DISPLAY'])
    red = [(xx, yy) for yy in range(y - 10, y + 12) for xx in range(x - 16, x + 17)
           if (lambda p: p[0] > 55 and p[0] > 2 * p[1] and p[0] > 1.8 * p[2])(image.getpixel((ox + xx, oy + yy))[:3])]
    assert red, 'No hostile skeleton visible'
    sx, sy = min(red, key=lambda p: (p[0] - statistics.median(v[0] for v in red)) ** 2 + (p[1] - statistics.median(v[1] for v in red)) ** 2)
    subprocess.run(['xdotool', 'mousemove', '--window', window, str(sx), str(sy), 'keydown', 'Shift_L', 'mousedown', '1'], check=True)
    time.sleep(3)
    subprocess.run(['xdotool', 'mouseup', '1', 'keyup', 'Shift_L'], check=True)
    state = command('dead')
    capture('t21-mouse-kill.png')
    a, b = state['player'], state['corpse']
    click(310 + 22 * (b['x'] - a['x'] - b['y'] + a['y']),
          250 + 22 * (b['x'] - a['x'] + b['y'] - a['y']) - 4 * (b['z'] - a['z']))
    command('opened')
    time.sleep(.6)
    subprocess.run(['xdotool', 'mousemove', '--window', window, '145', '180'], check=True)
    time.sleep(1.2)
    capture('t21-known-mould.png')
    click(145, 180)
    command('taken')
    command('live-final')
    capture('t21-mould-taken.png')
    print('Actual client: mouse kill -> declared Nightmare mould tooltip -> corpse grid pickup -> unchanged owned mould; rare hit seeded by fixture, gameplay uses production reward path.', flush=True)
