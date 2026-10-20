"""Actual client controls and movement for the generated treasure-dungeon loop."""
import json
import os
from pathlib import Path
import time
import subprocess

def exercise(window, click, capture):
    log = Path(os.environ['SANCTUARY_TEST_LOG'])
    def command(suffix):
        name = 'treasure-ui-' + suffix
        offset = log.stat().st_size
        os.write(int(os.environ['SANCTUARY_TEST_CONSOLE_FD']), (name+'\n').encode())
        end = time.monotonic()+15
        while time.monotonic()<end:
            text = log.read_text(errors='replace')[offset:]
            if name+': failed' in text:
                capture('t37-ui-failure.png')
                raise AssertionError(text[-3500:])
            if name+': passed' in text: return json.loads(Path(os.environ['SANCTUARY_TEST_UI_STATE']).read_text())
            time.sleep(.05)
        raise AssertionError('No response for '+name)
    click(335,367); command('setup'); time.sleep(.4)
    click(130,459); command('book'); click(90,327); command('quoted'); time.sleep(.4); capture('t37-key-recipe.png')
    click(90,349); time.sleep(5); command('crafted'); time.sleep(.6); capture('t37-deciphered-immunities.png')
    click(90,294); command('search'); time.sleep(5); command('ready'); time.sleep(1)
    subprocess.run(['xdotool','mousemove','--window',str(window),'150','180','click','3'],check=True); time.sleep(.3)
    capture('t37-dungeon-entry.png')
    subprocess.run(['xdotool','mousemove','--window',str(window),'398','250','mousedown','1'],check=True)
    time.sleep(.7)
    subprocess.run(['xdotool','mouseup','1'],check=True)
    time.sleep(1); command('walked'); capture('t37-dungeon-walking.png')
    command('chest'); time.sleep(.5); capture('t37-trove-chest.png'); command('final')
    print('Actual key crafting, map immunity disclosure, entrance search, generated-room movement, chest/key use and exit passed.', flush=True)
