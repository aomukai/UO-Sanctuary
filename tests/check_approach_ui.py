"""Measure noticing, approach and windup separately using the real client and AI."""
import json
import os
from pathlib import Path
import statistics
import time

ROOT = Path(__file__).resolve().parents[1]


def exercise(window, click, capture):
    log = ROOT / '.local/logs/britain-baseline-check.log'
    label = os.environ.get('SANCTUARY_APPROACH_LABEL', 'improved')
    reports = {}

    def command(suffix):
        name = 'approach-ui-' + suffix
        offset = log.stat().st_size
        os.write(int(os.environ['SANCTUARY_TEST_CONSOLE_FD']), (name + '\n').encode())
        deadline = time.monotonic() + 6
        while time.monotonic() < deadline:
            output = log.read_text(errors='replace')[offset:]
            if name + ': failed' in output:
                raise AssertionError(output[-2500:])
            if name + ': passed' in output:
                return json.loads(Path(os.environ['SANCTUARY_TEST_UI_STATE']).read_text())
            time.sleep(.025)
        raise AssertionError('Missing ' + name)

    try:
        for species in ('skeleton', 'zombie'):
            command(species)
            time.sleep(.6)
            command('go')
            click(332, 272)  # One accepted step puts the target inside the zombie approach cutoff.
            end = time.monotonic() + 18
            while True:
                state = command('state')
                if state['attack'] >= 0:
                    break
                assert time.monotonic() < end, (species, {k: v for k, v in state.items() if k != "samples"})
                time.sleep(.3)
            time.sleep(.7)
            state = command('arrived')
            moves = []
            previous = state['samples'][0]['enemy']
            for sample in state['samples']:
                if sample['enemy'] != previous:
                    moves.append(sample)
                    previous = sample['enemy']
            intervals = [b['t'] - a['t'] for a, b in zip(moves, moves[1:])]
            report = dict(state=state, acquire_ms=state['acquired'] - state['notice'],
                          first_move_ms=state['firstMove'] - state['notice'],
                          median_step_ms=statistics.median(intervals) if intervals else 0)
            reports[species] = report
            if label != 'baseline':
                assert 0 <= report['acquire_ms'] < 350, report
                assert 0 <= report['first_move_ms'] < 400, report
            assert 300 <= report['median_step_ms'] <= 360 if species == 'skeleton' else 930 <= report['median_step_ms'] <= 1020, report
            print(f"{label} {species}: acquired {report['acquire_ms']} ms, first move {report['first_move_ms']} ms, median step {report['median_step_ms']} ms", flush=True)
            capture(f'approach-{label}-{species}.png')
    finally:
        command('final')
    (ROOT / f'.local/logs/approach-{label}.json').write_text(json.dumps(reports, indent=2))
