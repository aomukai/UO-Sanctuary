"""Exercise actual insurance selection, quote, renewal, purchase and death."""
import os
from pathlib import Path
import time


def exercise(window, click, capture):
    raw_capture = capture

    def capture(name):
        time.sleep(.3)
        raw_capture(name)

    def command(suffix):
        name = 'insurance-ui-' + suffix
        log = Path(os.environ['SANCTUARY_TEST_LOG'])
        offset = log.stat().st_size
        os.write(int(os.environ['SANCTUARY_TEST_CONSOLE_FD']), (name + '\n').encode())
        deadline = time.monotonic() + 15
        while time.monotonic() < deadline:
            text = log.read_text(errors='replace')[offset:]
            if name + ': failed' in text:
                capture('t43-insurance-failure.png')
                raise AssertionError(text[-3000:])
            if name + ': passed' in text:
                return
            time.sleep(.05)
        raise AssertionError('Missing ' + name)

    command('setup')
    capture('t43-insurance-menu.png')
    for y in (180, 241, 302):
        click(540, y)
    click(350, 429)
    command('quoted')
    capture('t43-insurance-confirmation.png')
    click(62, 423)
    click(355, 86)
    command('renew-on')
    click(350, 429)
    click(350, 423)
    command('bought')
    capture('t43-insurance-paid.png')
    command('death')
    command('final')
    capture('t43-insurance-after-death.png')
    print('Actual client: insurance selection, charge preview/back, opt-in renewal, exact bank payment and real death protection/renewal passed.', flush=True)
