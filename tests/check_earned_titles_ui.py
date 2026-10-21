"""Exercise title selection, independent hides/ranks and the extended paperdoll text."""
import json
import os
from pathlib import Path
import time
import subprocess


def exercise(window, click, capture):
    def command(suffix):
        name = 'earned-title-ui-' + suffix
        log = Path(os.environ['SANCTUARY_TEST_LOG']); offset = log.stat().st_size
        os.write(int(os.environ['SANCTUARY_TEST_CONSOLE_FD']), (name + '\n').encode())
        deadline = time.monotonic() + 12
        while time.monotonic() < deadline:
            text = log.read_text(errors='replace')[offset:]
            if name + ': failed' in text:
                capture('t41-title-failure.png'); raise AssertionError(text[-3000:])
            if name + ': passed' in text:
                return json.loads(Path(os.environ['SANCTUARY_TEST_UI_STATE']).read_text())
            time.sleep(.05)
        raise AssertionError('Missing ' + name)
    click(335, 367); command('setup'); time.sleep(.4); click(410, 58); click(75, 318)
    command('opened'); command('guards'); time.sleep(.3); click(410, 58)
    capture('t41-title-selector.png')
    click(65, 171); click(330, 162); click(417, 398); click(75, 443); command('selected')
    capture('t41-titles-selected.png'); click(338, 443); command('preview'); time.sleep(.4)
    subprocess.run(['xdotool', 'mousemove', '--window', window, '130', '104', 'click', '--repeat', '2', '--delay', '120', '1'], check=True); time.sleep(.4)
    capture('t41-full-paperdoll-title.png')
    command('open'); time.sleep(.2); click(65, 121); click(330, 121); click(75, 443); command('hidden')
    click(330, 162); click(240, 398); click(75, 443); command('suffix')
    click(65, 196); click(330, 121); click(75, 443); command('prefix')
    click(65, 171); click(330, 162); click(417, 398); click(75, 443); command('selected')
    command('swap'); click(338, 443); time.sleep(.4); capture('t41-title-after-class-swap.png'); command('final')
    print('Actual client: wardrobe Titles entry, earned/locked choices, stale/forged guards, both independent hides, lower earned ranks, long full paperdoll text and class-independent selections passed.', flush=True)
