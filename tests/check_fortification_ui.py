"""Actual powder book, craft, cancellation and equipment fortification controls."""
import json
import os
from pathlib import Path
import time

def exercise(window, click, capture):
    log = Path(os.environ['SANCTUARY_TEST_LOG'])
    def command(suffix):
        name = 'fortification-ui-' + suffix
        offset = log.stat().st_size
        os.write(int(os.environ['SANCTUARY_TEST_CONSOLE_FD']), (name+'\n').encode())
        end = time.monotonic()+10
        while time.monotonic()<end:
            text=log.read_text(errors='replace')[offset:]
            if name+': failed' in text: raise AssertionError(text[-3500:])
            if name+': passed' in text: return json.loads(Path(os.environ['SANCTUARY_TEST_UI_STATE']).read_text())
            time.sleep(.05)
        raise AssertionError('No response for '+name)
    click(335,367); command('setup'); time.sleep(.4)
    click(130,322); click(90,414); command('quoted'); capture('t36-powder-recipe.png')
    click(389,294); command('cancelled')
    for attempt in range(20):
        click(130,322); click(90,414); command('quoted'); click(90,294); time.sleep(4)
        state=command('attempt')
        if state['powder'] is not None: break
    else: raise AssertionError('Twenty legitimate powder attempts produced no success')
    command('target'); capture('t36-fortify-confirm.png'); click(389,294); command('cancel-use')
    click(90,294); command('final'); capture('t36-fortified-unrepaired.png')
    print('Actual powder menu, cancellation, timed craft, equipment target, use cancellation and capped maximum-only fortification passed.', flush=True)
