"""Compare a real client's selected-target pursuit against native moving packets."""
import json
import os
from pathlib import Path
import re
import statistics
import subprocess
import time
from PIL import ImageGrab

ROOT = Path(__file__).resolve().parents[1]


def exercise(window, click, capture):
    serverlog = ROOT / '.local/logs/britain-baseline-check.log'
    clientlog = ROOT / '.local/logs/creation-ui-client.log'
    label = os.environ.get('SANCTUARY_PURSUIT_LABEL', 'improved')
    reports = {}

    def command(suffix):
        name = 'pursuit-ui-' + suffix
        offset = serverlog.stat().st_size
        os.write(int(os.environ['SANCTUARY_TEST_CONSOLE_FD']), (name + '\n').encode())
        deadline = time.monotonic() + 6
        while time.monotonic() < deadline:
            output = serverlog.read_text(errors='replace')[offset:]
            if name + ': failed' in output:
                raise AssertionError(output[-2500:])
            if name + ': passed' in output:
                return json.loads(Path(os.environ['SANCTUARY_TEST_UI_STATE']).read_text())
            time.sleep(.04)
        raise AssertionError('Missing ' + name)

    geometry = dict(line.split('=', 1) for line in subprocess.check_output(
        ['xdotool', 'getwindowgeometry', '--shell', window], text=True).splitlines() if '=' in line)
    for weapon in os.environ.get('SANCTUARY_PURSUIT_WEAPONS', 'dagger').split(','):
        state = command(weapon)
        time.sleep(1)
        a, b = state['player'], state['enemy']
        x = 310 + 22 * (b['x'] - a['x'] - b['y'] + a['y'])
        y = 190 + 22 * (b['x'] - a['x'] + b['y'] - a['y']) - 4 * (b['z'] - a['z'])
        im = ImageGrab.grab(xdisplay=os.environ['DISPLAY'])
        points = []
        for yy in range(y - 10, y + 11):
            for xx in range(x - 16, x + 17):
                r, g, blue = im.getpixel((int(geometry['X']) + xx, int(geometry['Y']) + yy))[:3]
                if r > 55 and r > 2 * g and r > 1.8 * blue:
                    points.append((xx, yy))
        assert points, f'No visible hostile at {(x, y)}'
        point = points[len(points) // 2]
        offset = clientlog.stat().st_size
        click(*point)
        state = command('go')
        time.sleep(13)
        state = command('arrived')
        capture(f'pursuit-{label}-{weapon}.png')
        samples = state['samples']
        starts = [b for a, b in zip(samples, samples[1:]) if b['next'] > a['next']]
        hits = [b for a, b in zip(samples, samples[1:]) if b['hp'] < a['hp']]
        distances = [max(abs(s['player']['x'] - s['enemy']['x']), abs(s['player']['y'] - s['enemy']['y'])) for s in samples]
        text = clientlog.read_text(errors='replace')[offset:]
        paths = [float(v) for v in re.findall(r'Movement path: ms=([\d.]+)', text)]
        ticks = [int(v) for v in re.findall(r'Pursuit path: tick=(\d+)', text)]
        intervals = [b - a for a, b in zip(ticks, ticks[1:])]
        assert paths and max(paths) < 100, paths
        assert not intervals or min(intervals) >= 200, intervals
        assert starts and hits, 'No attack starts or connected impacts'
        assert state['network']['QueueDepth'] == 0 and state['network']['PeakPacketRate'] <= 12, state['network']
        outages, began, caught = [], None, False
        for sample, distance in zip(samples, distances):
            if distance <= state['range']:
                if began is not None:
                    outages.append(sample['t'] - began)
                    began = None
                caught = True
            elif caught and began is None:
                began = sample['t']
        report = dict(state=state, starts=len(starts), hits=len(hits), mean_distance=statistics.mean(distances),
                      paths=len(paths), worst_path_ms=max(paths), min_replan_ms=min(intervals, default=None),
                      reacquire_ms=outages, target_steps=sum(s['moved'] for s in samples))
        reports[weapon] = report
        print(f"Pursuit {label} {weapon}: {report['starts']} starts, {report['hits']} impacts, mean distance {report['mean_distance']:.2f}, {len(paths)} paths / {max(paths):.2f} ms worst", flush=True)
    command('final')
    (ROOT / f'.local/logs/pursuit-{label}.json').write_text(json.dumps(reports, indent=2))
