"""Native wood harvest find message/tooltip and actual ClassicUO bark sale."""
import json
import os
from pathlib import Path
import subprocess
import time


def exercise(window, click, capture):
    log = Path(os.environ['SANCTUARY_TEST_LOG'])
    def command(suffix):
        name = 'runebark-ui-' + suffix
        offset = log.stat().st_size
        os.write(int(os.environ['SANCTUARY_TEST_CONSOLE_FD']), (name + '\n').encode())
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
    time.sleep(.8)
    capture('t22-harvest-find.png')
    command('sell')
    time.sleep(.8)
    subprocess.run(['xdotool', 'mousemove', '--window', window, '180', '90'], check=True)
    time.sleep(1.4)
    capture('t22-runebark-quote.png')
    subprocess.run(['xdotool', 'click', '--repeat', '2', '--delay', '80', '1'], check=True)
    time.sleep(.4)
    capture('t22-runebark-selected.png')
    click(395, 260)
    time.sleep(.7)
    command('sold')
    command('final')
    capture('t22-runebark-sold.png')
    print('Actual client: native wood harvest find; tier tooltip/80-gold quote; mouse sale; no bark stock. Rare ticket seeded in fixture.', flush=True)
