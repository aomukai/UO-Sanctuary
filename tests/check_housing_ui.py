"""Exercise actual GM measurement controls, town purchase/buyback and replacement."""
import os
from pathlib import Path
import subprocess
import time


def exercise(window, click, capture):
    raw_capture = capture

    def capture(name):
        time.sleep(.3)
        raw_capture(name)

    def command(suffix):
        name = 'housing-ui-' + suffix
        log = Path(os.environ['SANCTUARY_TEST_LOG'])
        offset = log.stat().st_size
        os.write(int(os.environ['SANCTUARY_TEST_CONSOLE_FD']), (name + '\n').encode())
        deadline = time.monotonic() + 15
        while time.monotonic() < deadline:
            text = log.read_text(errors='replace')[offset:]
            if name + ': failed' in text:
                capture('t42-housing-failure.png')
                raise AssertionError(text[-3000:])
            if name + ': passed' in text:
                return
            time.sleep(.05)
        capture('t42-housing-timeout.png')
        raise AssertionError('Missing ' + name)

    command('setup')
    click(215, 112)
    subprocess.run(['xdotool', 'key', '--window', window, 'ctrl+a'], check=True)
    subprocess.run(['xdotool', 'type', '--window', window, '3'], check=True)
    click(285, 115)
    for corner in range(4):
        command(f'floor-0-corner-{corner}')
        click(62, 155 + corner * 32)
    click(62, 155)  # Uncheck and recapture the first corner.
    command('floor-0-corner-0')
    click(62, 155)
    capture('t42-gm-first-floor.png')
    click(262, 392)
    for corner in range(4):
        command(f'floor-2-corner-{corner}')
        click(62, 155 + corner * 32)
    capture('t42-gm-upper-floor.png')
    command('exit')
    click(62, 320)
    click(62, 357)
    for corner in range(4):
        command(f'garden-corner-{corner}')
        click(62, 155 + corner * 32)
    capture('t42-gm-garden.png')
    click(62, 433)
    command('measured')
    click(62, 326)
    subprocess.run(['xdotool', 'key', '--window', window, 'Escape'], check=True)
    time.sleep(.3)
    command('author-open')
    click(62, 396)
    subprocess.run(['xdotool', 'key', '--window', window, 'Escape'], check=True)
    time.sleep(.3)
    command('author-open')
    click(62, 291)
    command('empty')
    capture('t42-gm-sale-settings.png')
    click(62, 428)
    command('listed')
    capture('t42-player-purchase.png')
    click(62, 253)
    command('bought')
    capture('t42-owner-options.png')
    click(62, 248)
    command('manage')
    capture('t42-native-house-management.png')
    command('blocked-setup')
    click(62, 293)
    capture('t42-buyback-confirmation.png')
    click(62, 283)
    command('blocked')
    click(62, 293)
    click(62, 283)
    command('sold')
    command('replacement')
    capture('t42-replacement-wardrobe.png')
    click(345, 388)
    command('final')
    print('Actual client: corner capture/redo, inherited and measured floors, garden, furnished/empty, listing, purchase, native management, blocked/empty buyback and paid replacement passed.', flush=True)
