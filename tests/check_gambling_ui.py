"""Exercise real vendor speech, hidden stock and paid purchase in ClassicUO."""
import os
from pathlib import Path
import subprocess
import time


def exercise(window, click, capture):
    def command(suffix):
        name = 'gambling-ui-' + suffix
        log = Path(os.environ['SANCTUARY_TEST_LOG'])
        offset = log.stat().st_size
        os.write(int(os.environ['SANCTUARY_TEST_CONSOLE_FD']), (name + '\n').encode())
        deadline = time.monotonic() + 15
        while time.monotonic() < deadline:
            text = log.read_text(errors='replace')[offset:]
            if name + ': failed' in text:
                capture('t45-gambling-failure.png')
                raise AssertionError(text[-3000:])
            if name + ': passed' in text:
                return
            time.sleep(.05)
        raise AssertionError('Missing ' + name)

    def opened():
        subprocess.run(['xdotool', 'key', '--window', window, 'Return'], check=True)
        subprocess.run(['xdotool', 'type', '--window', window, 'vendor buy'], check=True)
        subprocess.run(['xdotool', 'key', '--window', window, 'Return'], check=True)
        time.sleep(.5)
        command('opened')

    command('setup')
    opened()
    capture('t45-gambling-hidden.png')
    click(350, 458)
    command('next-page')
    capture('t45-gambling-second-page.png')
    click(215, 458)
    command('previous-page')
    click(535, 149)
    command('no-funds')
    click(495, 458)
    command('closed')
    command('fund')
    opened()
    command('full')
    click(535, 149)
    command('full-kept')
    command('room')
    click(535, 149)
    command('bought')
    capture('t45-gambling-purchased.png')
    command('refund-prepare')
    click(535, 189)
    command('refunded')
    capture('t45-gambling-refunded.png')
    command('stale')
    opened()
    click(65, 458)
    capture('t45-gambling-refreshed.png')
    command('named-prepare')
    click(535, 189)
    command('named-bought')
    capture('t49-gambling-unique-purchased.png')
    command('final')
    if os.environ.get('SANCTUARY_TEST_NAMED_VISUALS') == '1':
        import json
        subprocess.run(['xdotool', 'mousemove', '--window', window, '510', '500', 'click', '3'], check=True)
        time.sleep(.2)
        def visual(suffix, opacity):
            name = 'named-visual-ui-' + suffix
            log = Path(os.environ['SANCTUARY_TEST_LOG']); offset = log.stat().st_size
            os.write(int(os.environ['SANCTUARY_TEST_CONSOLE_FD']), (name + '\n').encode())
            deadline = time.monotonic() + 12
            while time.monotonic() < deadline:
                text = log.read_text(errors='replace')[offset:]
                if name + ': failed' in text: raise AssertionError(text[-3000:])
                if name + ': passed' in text:
                    scene = json.loads(Path(os.environ['SANCTUARY_NAMED_VISUAL_SERVER_STATE']).read_text())
                    trace = Path(os.environ['SANCTUARY_NAMED_VISUAL_TRACE'])
                    if trace.exists() and trace.read_text().split() == [str(scene['Serial']), str(opacity)]:
                        assert not scene['Hidden'] and scene['Opacity'] == opacity
                        time.sleep(.5)
                        capture('t49-natalya-' + suffix + '.png')
                        return
                time.sleep(.05)
            raise AssertionError('Named opacity did not reach actual client: ' + name)
        visual('setup', 128)
        visual('opaque', 255)
        visual('faded', 128)
        visual('golden', 255)
        time.sleep(2.2)
        capture('t49-golden-set-aura.png')
        visual('blood', 255)
        print('Actual client: visible Natalya fade 128 -> opaque 255 -> faded 128 passed.', flush=True)
    print('Actual client: vendor speech, hidden stock, no-funds/full-pack safety, purchase/reveal, stale/replay safety and refresh passed.', flush=True)
