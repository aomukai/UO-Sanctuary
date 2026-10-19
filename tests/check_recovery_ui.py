"""Verify hit recovery pauses real predicted movement and retains/releases input."""
import json
import os
from pathlib import Path
import subprocess
import time

ROOT = Path(__file__).resolve().parents[1]


def exercise(window, click, capture):
    log = ROOT / '.local/logs/britain-baseline-check.log'
    reports = {}

    def command(suffix):
        name = 'recovery-ui-' + suffix
        offset = log.stat().st_size
        os.write(int(os.environ['SANCTUARY_TEST_CONSOLE_FD']), (name + '\n').encode())
        deadline = time.monotonic() + 6
        while time.monotonic() < deadline:
            text = log.read_text(errors='replace')[offset:]
            if name + ': failed' in text:
                raise AssertionError(text[-2400:])
            if name + ': passed' in text:
                return json.loads(Path(os.environ['SANCTUARY_TEST_UI_STATE']).read_text())
            time.sleep(.02)
        raise AssertionError('Missing ' + name)

    def button(action):
        subprocess.run(['xdotool', action, '1'], check=True)

    def hold():
        subprocess.run(['xdotool', 'mousemove', '--window', window, '450', '320'], check=True)
        time.sleep(.12)
        button('mousedown')

    try:
        command('setup')
        time.sleep(.7)
        command('zombie')
        time.sleep(.44)
        reports['zombie'] = command('zombie-hit')
        capture('hit-recovery-zombie.png')
        time.sleep(.6)
        hold()
        time.sleep(.5)
        weak = command('weak')
        time.sleep(.5)
        before = command('state')
        assert len(before['moves']) >= len(weak['moves']) + 2, 'Weak hit stopped held movement'
        strong = command('strong')
        time.sleep(.10)
        command('repeat')
        time.sleep(.10)
        command('locked')
        capture('hit-recovery-locked.png')
        time.sleep(.6)
        resumed = command('resumed')
        hit = strong['hits'][-1]
        after = [m for m in resumed['moves'] if m['t'] >= hit['t']]
        assert after and hit['until'] <= after[0]['t'] <= hit['until'] + 300, (hit, after)
        reports['single'] = resumed
        time.sleep(.2)
        command('pack')
        time.sleep(2.2)
        pack = command('state')
        assert len(pack['hits']) >= len(resumed['hits']) + 2, 'Staggered hits failed to chain recovery'
        assert pack['network']['PeakPacketRate'] <= 12 and pack['network']['QueueDepth'] == 0, pack['network']
        reports['pack'] = pack
        command('strong')
        button('mouseup')
        time.sleep(.9)
        reports['release'] = command('released')
        print(f"Hit recovery: weak hit preserves movement; Necromancer resumes {after[0]['t'] - hit['t']} ms after strong hit; pack chains {len(pack['hits']) - len(resumed['hits'])} recoveries; release stays stopped.", flush=True)
    finally:
        button('mouseup')
        command('final')
    (ROOT / '.local/logs/hit-recovery-ui.json').write_text(json.dumps(reports, indent=2))
