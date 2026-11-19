"""Real creation controls for both aliases, followed by packet/sex/title checks."""
import os
from pathlib import Path
import subprocess
import time

ROOT = Path(os.environ.get('SANCTUARY_TEST_RUNTIME_ROOT', Path(__file__).resolve().parents[1]))


def exercise(window, click, capture, character, wait_for):
    def choose(index):
        click(135 + 184 * (index % 2), 112 + 67 * (index // 2))

    female = os.environ.get('SANCTUARY_TEST_CLASS_NAMES_FEMALE') == '1'
    default_id = 106 if female else 102
    class_id = int(os.environ.get('SANCTUARY_TEST_CLASS_NAMES_ID', default_id))
    assert 101 <= class_id <= 108
    click(326 if female else 190, 146)
    click(540, 408)
    click(310, 194)
    subprocess.run(['xdotool', 'key', '--window', window, 'ctrl+a'], check=True)
    subprocess.run(['xdotool', 'type', '--window', window, '--delay', '60', character], check=True)
    click(540, 408)
    suffix = 'female' if female else 'male'
    capture(f't03-class-choices-{suffix}.png')
    for index in range(8):
        choose(index)
        capture(f't03-creation-{101 + index}-{suffix}.png')
    click(88, 408)
    capture('t03-back-preserved-name.png')
    click(540, 408)
    choose(class_id - 101)
    click(540, 408)
    wait_for('Login step: CharacterCreationDone')
    time.sleep(2)
    click(130, 419)
    capture(f't03-paperdoll-{suffix}.png')
    click(150, 453)  # Real paperdoll Class button.
    time.sleep(.4)
    capture(f't03-talents-{suffix}.png')
    log = Path(os.environ.get('SANCTUARY_TEST_LOG', ROOT / '.local/logs/britain-baseline-check.log'))
    offset = log.stat().st_size
    os.write(int(os.environ['SANCTUARY_TEST_CONSOLE_FD']), b'class-names-ui-check\n')
    deadline = time.monotonic() + 6
    while time.monotonic() < deadline:
        output = log.read_text(errors='replace')[offset:]
        if 'class-names-ui-check: failed' in output:
            raise AssertionError(output[-2500:])
        if 'class-names-ui-check: passed' in output:
            print(f'Actual client: all eight class choices and Back/reselection. Submitted '
                  f'wire class {class_id}, female={female}, with matching paperdoll/talent heading.', flush=True)
            return
        time.sleep(.05)
    raise AssertionError('Missing class-names-ui-check response')
