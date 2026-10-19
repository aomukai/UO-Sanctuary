"""Actual ClassicUO shop buy/sell, displayed-item delivery and redacted tooltips."""
import json
import os
from pathlib import Path
import subprocess
import time

ROOT = Path(__file__).resolve().parents[1]


def exercise(window, click, capture):
    logfile = Path(os.environ['SANCTUARY_TEST_LOG'])
    def command(name):
        offset = logfile.stat().st_size
        os.write(int(os.environ['SANCTUARY_TEST_CONSOLE_FD']), ('market-ui-' + name + '\n').encode())
        end = time.monotonic() + 6
        while time.monotonic() < end:
            text = logfile.read_text(errors='replace')[offset:]
            if f'market-ui-{name}: failed' in text:
                raise AssertionError(text[-2000:])
            if f'market-ui-{name}: passed' in text:
                return json.loads(Path(os.environ['SANCTUARY_TEST_UI_STATE']).read_text())
            time.sleep(.05)
        raise AssertionError('Missing market-ui-' + name)
    click(335, 367)
    command('setup')
    time.sleep(.8)
    capture('t11-shop-buy.png')
    subprocess.run(['xdotool', 'mousemove', '--window', window, '230', '95'], check=True)
    time.sleep(1.5)
    capture('t11-shop-buy-tooltip.png')
    subprocess.run(['xdotool', 'mousemove', '--window', window, '230', '95', 'click', '--repeat', '2', '--delay', '80', '1'], check=True)
    time.sleep(.4)
    capture('t11-shop-buy-selected.png')
    click(445, 265)
    time.sleep(.6)
    command('bought')
    command('sell')
    time.sleep(.8)
    capture('t11-shop-sell.png')
    subprocess.run(['xdotool', 'mousemove', '--window', window, '180', '90'], check=True)
    time.sleep(1.5)
    capture('t11-shop-unidentified-tooltip.png')
    subprocess.run(['xdotool', 'mousemove', '--window', window, '180', '90', 'click', '--repeat', '2', '--delay', '80', '1'], check=True)
    time.sleep(.4)
    capture('t11-shop-sell-selected.png')
    click(395, 260)
    time.sleep(.6)
    command('sold')
    command('final')
    print('Actual shop client: visible stock stats, exact displayed-item purchase, hidden unidentified stats, correct sale payment and no buyback passed.', flush=True)
