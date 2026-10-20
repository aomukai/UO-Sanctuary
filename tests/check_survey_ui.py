"""Real Cartography book, consumed map, discounted price and stale-quote rejection."""
import json
import os
from pathlib import Path
import subprocess
import time


def exercise(window, click, capture):
    log = Path(os.environ['SANCTUARY_TEST_LOG'])
    console = int(os.environ['SANCTUARY_TEST_CONSOLE_FD'])

    def command(suffix):
        name = 'survey-ui-' + suffix
        offset = log.stat().st_size
        os.write(console, (name + '\n').encode())
        deadline = time.monotonic() + 12
        while time.monotonic() < deadline:
            text = log.read_text(errors='replace')[offset:]
            if name + ': failed' in text:
                raise AssertionError(text[-3500:])
            if name + ': passed' in text:
                return json.loads(Path(os.environ['SANCTUARY_TEST_UI_STATE']).read_text())
            time.sleep(.05)
        raise AssertionError('No response for ' + name)

    def buy():
        subprocess.run(['xdotool', 'mousemove', '--window', window, '230', '95', 'click', '--repeat', '2', '--delay', '80', '1'], check=True)
        time.sleep(.4)
        click(445, 265)
        time.sleep(.5)

    click(335, 367)
    command('setup')
    click(130, 322)
    capture('t32-map-book.png')
    click(75, 105)
    command('quoted')
    capture('t32-map-confirm.png')
    click(600, 55)
    command('cancelled')
    click(130, 322)
    click(75, 105)
    click(75, 410)
    time.sleep(4)
    command('crafted')
    capture('t32-map-inventory.png')
    subprocess.run(['xdotool', 'mousemove', '--window', window, '122', '132', 'click', '--repeat', '2', '--delay', '100', '1'], check=True)
    time.sleep(.4)
    command('used')
    command('shop')
    time.sleep(.8)
    capture('t32-discount-380.png')
    buy()
    command('bought')
    command('stale-shop')
    command('expire')
    buy()
    command('stale-rejected')
    capture('t32-stale-price-rejected.png')
    command('reopen')
    time.sleep(.6)
    capture('t32-ordinary-400.png')
    buy()
    command('final')
    print('Actual map book/cancel/native craft/use; discounted shop 400->380, expired quote rejected without charge, reopened 400 paid exactly.', flush=True)
