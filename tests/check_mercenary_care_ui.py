"""Click the real client mercenary care quote, cancellation and payment."""
import os
from pathlib import Path
import time


def exercise(window, click, capture):
    def command(suffix):
        name = 'mercenary-ui-' + suffix
        log = Path(os.environ['SANCTUARY_TEST_LOG'])
        offset = log.stat().st_size
        os.write(int(os.environ['SANCTUARY_TEST_CONSOLE_FD']), (name + '\n').encode())
        deadline = time.monotonic() + 15
        while time.monotonic() < deadline:
            text = log.read_text(errors='replace')[offset:]
            if name + ': failed' in text:
                capture('t46-care-failure.png')
                raise AssertionError(text[-3000:])
            if name + ': passed' in text:
                time.sleep(.3)
                return
            time.sleep(.05)
        raise AssertionError('Missing ' + name)

    command('setup')
    capture('t46-care-quote.png')
    click(90, 248)
    command('insufficient')
    click(402, 248)
    command('cancelled')
    capture('t46-care-funded-quote.png')
    click(90, 248)
    command('paid')
    capture('t46-care-resurrected.png')
    command('auto-start')
    time.sleep(2)
    command('auto-first')
    time.sleep(2)
    command('auto-cooldown')
    time.sleep(9)
    command('final')
    print('Actual client: care quote, insufficient funds, cancel and exact resurrection payment retained gear and potion reserve.', flush=True)
