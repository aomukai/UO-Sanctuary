"""Actual repair menus, cancellation, player repair and NPC recharge controls."""
import json
import os
from pathlib import Path
import subprocess
import time


def exercise(window, click, capture):
    logfile = Path(os.environ['SANCTUARY_TEST_LOG'])

    def command(name):
        offset = logfile.stat().st_size
        os.write(int(os.environ['SANCTUARY_TEST_CONSOLE_FD']), ('repair-ui-' + name + '\n').encode())
        end = time.monotonic() + 8
        while time.monotonic() < end:
            text = logfile.read_text(errors='replace')[offset:]
            if f'repair-ui-{name}: failed' in text:
                raise AssertionError(text[-2400:])
            if f'repair-ui-{name}: passed' in text:
                return json.loads(Path(os.environ['SANCTUARY_TEST_UI_STATE']).read_text())
            time.sleep(.05)
        raise AssertionError('Missing repair-ui-' + name)

    click(335, 367)
    command('setup')
    time.sleep(1.5)
    capture('t12-repair-tool.png')
    click(125, 427)
    time.sleep(.3)
    command('target')
    time.sleep(.5)
    capture('t12-repair-actions.png')
    click(60, 162)
    time.sleep(.5)
    capture('t12-repair-quote.png')
    click(423, 427)
    time.sleep(.3)
    command('cancelled')
    time.sleep(.4)
    click(60, 162)
    time.sleep(.4)
    click(60, 427)
    time.sleep(.5)
    capture('t12-repair-result.png')
    command('repaired')
    command('npc')
    time.sleep(.6)
    capture('t12-npc-recharge.png')
    click(280, 245)
    subprocess.run(['xdotool', 'key', '--window', window, 'ctrl+a'], check=True)
    subprocess.run(['xdotool', 'type', '--window', window, '2'], check=True)
    click(60, 277)
    time.sleep(.5)
    capture('t12-npc-charge-quote.png')
    click(60, 427)
    time.sleep(.5)
    command('quantity')
    time.sleep(.5)
    click(340, 317)
    time.sleep(.5)
    capture('t12-npc-combined-quote.png')
    click(60, 427)
    time.sleep(.5)
    command('final')
    print('Actual repair client: tool menu/target, cost/risk confirmation, cancellation, broken repair, NPC quantity recharge and combined repair/fill passed.', flush=True)
