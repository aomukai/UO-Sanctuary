"""Real creation controls for both aliases, followed by packet/sex/title checks."""
import os
from pathlib import Path
import subprocess
import time

ROOT = Path(__file__).resolve().parents[1]


def exercise(window, click, capture, character, wait_for):
    capture('t03-class-choices.png')

    def choose(index):
        click(210 + 200 * (index % 2), 205 + 55 * (index // 2))

    for index in range(7):
        choose(index)
        click(435, 445)  # Native Male radio.
        capture(f't03-creation-{101 + index}-male.png')
        click(435, 465)  # Native Female radio.
        capture(f't03-creation-{101 + index}-female.png')
        click(596, 455)  # Back; appearance and sex remain selected.
        choose(index)
        capture(f't03-back-preserved-{101 + index}.png')
        click(596, 455)

    female = os.environ.get('SANCTUARY_TEST_CLASS_NAMES_FEMALE') == '1'
    choose(5 if female else 1)  # Druidess or Sorcerer, exercising non-enum titles.
    click(435, 465 if female else 445)
    click(320, 82)
    subprocess.run(['xdotool', 'key', '--window', window, 'ctrl+a'], check=True)
    subprocess.run(['xdotool', 'type', '--window', window, '--delay', '60', character], check=True)
    click(615, 455)
    wait_for('Login step: CharacterCreationDone')
    time.sleep(1)
    suffix = 'female' if female else 'male'
    capture(f't03-paperdoll-{suffix}.png')
    click(320, 263)  # Real paperdoll Class button.
    time.sleep(.4)
    capture(f't03-talents-{suffix}.png')
    log = ROOT / '.local/logs/britain-baseline-check.log'
    offset = log.stat().st_size
    os.write(int(os.environ['SANCTUARY_TEST_CONSOLE_FD']), b'class-names-ui-check\n')
    deadline = time.monotonic() + 6
    while time.monotonic() < deadline:
        output = log.read_text(errors='replace')[offset:]
        if 'class-names-ui-check: failed' in output:
            raise AssertionError(output[-2500:])
        if 'class-names-ui-check: passed' in output:
            print('Actual client: all seven class choices, Male/Female toggles, Back/reselection; submitted '
                  + ('Druidess' if female else 'Sorcerer') + ' with matching paperdoll/talent heading.', flush=True)
            return
        time.sleep(.05)
    raise AssertionError('Missing class-names-ui-check response')
