"""Actual low-stamina allied path and real five-second overlap recovery."""
import json
import os
from pathlib import Path
import subprocess
import time


def exercise(window, click, capture):
    def command(suffix):
        name='allied-ui-'+suffix
        log=Path(os.environ['SANCTUARY_TEST_LOG']); offset=log.stat().st_size
        os.write(int(os.environ['SANCTUARY_TEST_CONSOLE_FD']),(name+'\n').encode())
        deadline=time.monotonic()+12
        while time.monotonic()<deadline:
            text=log.read_text(errors='replace')[offset:]
            if name+': failed' in text: raise AssertionError(text[-2500:])
            if name+': passed' in text: return json.loads(Path(os.environ['SANCTUARY_TEST_UI_STATE']).read_text())
            time.sleep(.05)
        raise AssertionError(name+' timed out')

    def key(k):
        subprocess.run(['xdotool','key','--window',window,k],check=True); time.sleep(.3)

    click(335,367);command('setup');time.sleep(.7)
    # Six eastward tiles, beyond the guardian; the direct path must cross it.
    click(442,380);time.sleep(3.6);capture('allied-transit.png');command('walked')
    command('overlap');time.sleep(.4);key('1');command('blocked');capture('allied-walk-only.png')
    command('owner-holds');time.sleep(.4);key('1');command('opened');capture('allied-holder-active.png')
    time.sleep(5.2);command('regrouped');capture('allied-regrouped.png');command('final')
    print('Actual low-stamina client path crossed ally, blocked only the passer, kept the holder active and regrouped the passing pet at five seconds.',flush=True)
