"""Real crafting book, tome targets/charges, belt portal and return controls."""
import json
import os
from pathlib import Path
import subprocess
import time


def exercise(window, click, capture):
    def command(suffix):
        name='inscription-ui-'+suffix
        log=Path(os.environ['SANCTUARY_TEST_LOG']); offset=log.stat().st_size
        os.write(int(os.environ['SANCTUARY_TEST_CONSOLE_FD']),(name+'\n').encode())
        end=time.monotonic()+12
        while time.monotonic()<end:
            text=log.read_text(errors='replace')[offset:]
            if name+': failed' in text: raise AssertionError(text[-3000:])
            if name+': passed' in text: return json.loads(Path(os.environ['SANCTUARY_TEST_UI_STATE']).read_text())
            time.sleep(.05)
        raise AssertionError(name+' did not respond')

    def mouse(x,y,button=3,double=False):
        subprocess.run(['xdotool','mousemove','--window',window,str(x),str(y)],check=True);time.sleep(.15)
        args=['xdotool','click']
        if double: args+=['--repeat','2','--delay','100']
        subprocess.run(args+[str(button)],check=True);time.sleep(.4)

    def key(k):
        subprocess.run(['xdotool','key','--window',window,k],check=True);time.sleep(.3)

    click(335,367);command('setup');click(130,322)
    capture('t34-inscription-book.png')
    click(75,195);command('quoted');capture('t34-tome-confirm.png')
    click(600,55);command('cancelled');click(130,322);click(75,195);click(75,455)
    time.sleep(4);capture('t34-after-craft.png');command('crafted');capture('t34-tome-inventory.png');time.sleep(1.2)
    mouse(126,138);capture('t34-tome-target.png');command('target');key('Escape');command('cancel-target');time.sleep(1.2)
    mouse(126,138);capture('t34-tome-target.png');command('target');click(176,145);command('identified')
    click(458,439);click(207,133);command('loaded');command('field');time.sleep(.6)
    key('1');state=command('portal');capture('t34-field-portal.png')
    # World feet at (310,250); target the center of the portal sprite.
    x=310+(state['dx']-state['dy'])*22
    y=250+(state['dx']+state['dy'])*22-state['dz']*4-45
    mouse(x,y,1,True);capture('t34-portal-entry-click.png');command('town');capture('t34-town-portal.png')
    time.sleep(1.2);mouse(310,180,1,True);capture('t34-portal-return-click.png');command('final');capture('t34-portal-return.png')
    print('Actual Inscription quote/cancel/native craft, tome right-click/cancel/identify, belt portal, town and return passed.',flush=True)
