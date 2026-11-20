"""Exercise banker interaction and escrow recovery, deferral, and forfeiture."""
import os
from pathlib import Path
import time


def exercise(window, click, capture):
    raw_capture = capture

    def capture(name):
        time.sleep(.3)
        raw_capture(name)

    def command(suffix):
        name = 'escrow-ui-' + suffix
        log = Path(os.environ['SANCTUARY_TEST_LOG'])
        offset = log.stat().st_size
        os.write(int(os.environ['SANCTUARY_TEST_CONSOLE_FD']), (name + '\n').encode())
        deadline = time.monotonic() + 15
        while time.monotonic() < deadline:
            text = log.read_text(errors='replace')[offset:]
            if name + ': failed' in text:
                capture('t44-escrow-failure.png')
                raise AssertionError(text[-3000:])
            if name + ': passed' in text:
                return
            time.sleep(.05)
        raise AssertionError('Missing ' + name)

    def open_bank_recovery():
        time.sleep(.4)
        click(342, 219)
        click(406, 419)
        command('opened')

    click(605, 101)  # Close the initial character sheet before clicking the banker.
    command('setup')
    open_bank_recovery()
    capture('t44-escrow-claims.png')
    click(410, 172)
    capture('t44-escrow-contents.png')
    click(330, 420)
    command('no-funds')
    command('fund')
    click(410, 172)
    click(62, 453)
    command('closed')
    open_bank_recovery()
    command('full')
    click(410, 172)
    click(330, 420)
    command('full-kept')
    command('room')
    click(410, 172)
    click(330, 420)
    command('recovered')
    capture('t44-escrow-recovered.png')
    click(410, 172)
    click(330, 453)
    command('confirm-shown')
    capture('t44-escrow-forfeit-confirm.png')
    click(62, 435)
    command('kept')
    click(330, 453)
    command('confirm-shown')
    click(325, 435)
    command('final')
    capture('t44-escrow-final.png')
    print('Actual client: banker dialogue, no-funds/full-bank safety, close/reopen, exact nested recovery and explicit/backed-out forfeiture passed.', flush=True)
