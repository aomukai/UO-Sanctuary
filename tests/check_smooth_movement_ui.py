"""Measure real-client routes and steering in a disposable world."""
import json
import math
import os
from pathlib import Path
import statistics
import subprocess
import time

ROOT = Path(__file__).resolve().parents[1]


def exercise(window, click, capture):
    recorder = None
    if os.environ.get('SANCTUARY_TEST_MOVEMENT_VIDEO') == '1':
        geometry = dict(line.split('=', 1) for line in subprocess.check_output(
            ['xdotool', 'getwindowgeometry', '--shell', window], text=True).splitlines() if '=' in line)
        label = 'baseline' if os.environ.get('SANCTUARY_TEST_SMOOTH_BASELINE') == '1' else 'improved'
        with (ROOT / '.local/logs/smooth-video.log').open('w') as output:
            recorder = subprocess.Popen(['ffmpeg', '-nostdin', '-y', '-f', 'x11grab', '-framerate', '30',
                '-video_size', geometry['WIDTH'] + 'x' + geometry['HEIGHT'],
                '-i', os.environ['DISPLAY'] + '+' + geometry['X'] + ',' + geometry['Y'],
                '-c:v', 'libx264', '-preset', 'ultrafast', '-crf', '24', '-pix_fmt', 'yuv420p',
                str(ROOT / f'.local/logs/smooth-{label}.mp4')], stdout=output, stderr=subprocess.STDOUT)
    try:
        _exercise(window, click, capture)
    finally:
        subprocess.run(['xdotool', 'mouseup', '1'], check=False)
        if recorder:
            import signal
            recorder.send_signal(signal.SIGINT)
            recorder.wait(timeout=10)


def _exercise(window, click, capture):
    serverlog = ROOT / '.local/logs/britain-baseline-check.log'
    clientlog = ROOT / '.local/logs/creation-ui-client.log'
    baseline = os.environ.get('SANCTUARY_TEST_SMOOTH_BASELINE') == '1'
    label = 'baseline' if baseline else 'improved'
    reports = {}
    selected = os.environ.get('SANCTUARY_TEST_SMOOTH_CASES', '').split(',')
    def enabled(name):
        return selected == [''] or name in selected

    def command(suffix):
        name = 'smooth-ui-' + suffix
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

    def mouse(x, y):
        subprocess.run(['xdotool', 'mousemove', '--window', window, str(round(x)), str(round(y))], check=True)
        time.sleep(.08)  # Picking consumes mouse motion on the next game frame.

    def button(action):
        subprocess.run(['xdotool', action, '1'], check=True)

    def aim(state):
        a, b = state['player'], state['goal']
        mouse(310 + 22 * (b['x'] - a['x'] - b['y'] + a['y']),
              250 + 22 * (b['x'] - a['x'] + b['y'] - a['y']) - 4 * (b['z'] - a['z']))

    def report(name, state):
        prev = state['origin']
        moves, turns = [], 0
        last_dir = 2
        for sample in state['samples']:
            if (sample['x'], sample['y'], sample['z']) == (prev['x'], prev['y'], prev['z']):
                turns += 1
            else:
                moves.append(sample)
                turns += (sample['dir'] & 7) != last_dir
                last_dir = sample['dir'] & 7
            prev = sample
        intervals = [b['t'] - a['t'] for a, b in zip(moves, moves[1:])]
        a, b = state['origin'], state['goal']
        dx, dy = b['x'] - a['x'], b['y'] - a['y']
        cross = [abs((p['x'] - a['x']) * dy - (p['y'] - a['y']) * dx) / math.hypot(dx, dy) for p in moves] if math.hypot(dx, dy) and name not in ('steering-reversal-release', 'sustained-hold') else []
        reports[name] = {'state': state, 'steps': len(moves), 'turns': turns,
                         'median_interval_ms': statistics.median(intervals) if intervals else 0,
                         'max_interval_ms': max(intervals, default=0),
                         'max_line_error_tiles': max(cross) if cross else None}
        line_report = f", max line error {max(cross):.2f} tiles" if cross else ''
        print(f"{label} {name}: {len(moves)} steps, {turns} turns, median {statistics.median(intervals) if intervals else 0:.0f} ms{line_report}", flush=True)
        return moves

    for name in ['straight', 'angle-a', 'angle-b', 'angle-c', 'angle-d']:
        if not enabled(name):
            continue
        state = command(name)
        time.sleep(.7)
        aim(state)
        time.sleep(.15)
        button('click')
        time.sleep(3.5)
        capture(f'smooth-{label}-{name}-before-check.png')
        state = command('arrived')
        moves = report(name, state)
        if not baseline and name.startswith('angle'):
            assert reports[name]['max_line_error_tiles'] <= .6, reports[name]
            assert reports[name]['max_interval_ms'] < 270, reports[name]
        capture(f'smooth-{label}-{name}.png')

    if enabled('walk-angle'):
        state = command('angle-a')
        time.sleep(.7)
        subprocess.run(['xdotool', 'key', '--window', window, 'Tab'], check=True)
        time.sleep(.2)
        try:
            aim(state)
            button('click')
            time.sleep(3.6)
            state = command('arrived')
            moves = report('walk-angle', state)
            assert moves and all((p['dir'] & 128) == 0 for p in moves), state
            assert 375 <= reports['walk-angle']['median_interval_ms'] <= 450
        finally:
            subprocess.run(['xdotool', 'key', '--window', window, 'Tab'], check=True)
            time.sleep(.2)

    # Fixed screen offsets keep a moving goal ahead of the player.
    command('straight')
    time.sleep(.7)
    offset = clientlog.stat().st_size
    mouse(450, 320)
    button('mousedown')
    try:
        time.sleep(.9)
        for point in [(465, 305), (475, 285), (480, 265), (475, 245)]:
            mouse(*point)
            time.sleep(.28)
        reverse_mark = command('state')
        mouse(180, 200)
        time.sleep(.8)
        reverse_state = command('state')
        previous_x = reverse_mark['player']['x']
        reverse_times = []
        for p in reverse_state['samples'][len(reverse_mark['samples']):]:
            if p['x'] < previous_x:
                reverse_times.append(p['t'] - reverse_mark['elapsed'])
            previous_x = p['x']
        assert reverse_times and min(reverse_times) <= (650 if baseline else 450), (reverse_times, reverse_state)
        mouse(450, 320)
        time.sleep(.8)
        release_mark = command('state')
    finally:
        button('mouseup')
    time.sleep(.5)
    command('mark')
    time.sleep(.5)
    state = command('stopped')
    report('steering-reversal-release', state)
    last_step_after_release = max((p['t'] - release_mark['elapsed'] for p in state['samples']), default=0)
    assert last_step_after_release <= 250, state
    reports['steering-reversal-release'].update({'reversal_ms': min(reverse_times), 'last_step_after_release_ms': max(0, last_step_after_release)})
    trace = clientlog.read_text(errors='replace')[offset:]
    assert 'Unhandled exception' not in trace

    if not baseline:
        for name in ['wall', 'fence', 'corner', 'door', 'stairs']:
            if not enabled(name):
                continue
            state = command(name)
            time.sleep(.7)
            if name == 'door':
                # Closed doorway stays authoritative; then use the opened passage.
                aim(state)
                log_start = clientlog.stat().st_size
                button('mousedown')
                time.sleep(6)
                button('mouseup')
                time.sleep(.5)
                state = command('state')
                import re
                durations = [float(v) for v in re.findall(r'Movement path: ms=([\d.]+)', clientlog.read_text(errors='replace')[log_start:])]
                assert durations and max(durations) < 100
                reports['closed-door-hold'] = {'routes': len(durations), 'worst_search_ms': max(durations), 'state': state}
                print(f"Closed-door hold: {len(durations)} routes; worst {max(durations):.2f} ms", flush=True)
                assert all(not (p['x'] == state['origin']['x'] + 3 and abs(p['y'] - state['origin']['y']) <= 12) for p in state['samples']), state
                subprocess.run(['xdotool', 'keydown', 'Shift_L'], check=True)
                time.sleep(.2)
                state = command('door-open')
                subprocess.run(['xdotool', 'keyup', 'Shift_L'], check=True)
                time.sleep(.3)
                state = command('state')
            aim(state)
            if name == 'stairs':
                # Aim at natural ground beyond the dynamic stair surfaces, then
                # stop on the steps and click back down to the original ground.
                button('mousedown')
                time.sleep(1.05)
                button('mouseup')
                time.sleep(.5)
                state = command('elevated')
                report(name, state)
                capture('smooth-stairs-ascent.png')
                state = command('descend')
                aim(state)
                button('click')
                time.sleep(3)
                state = command('arrived')
                assert state['player']['z'] == state['origin']['z']
                report('stairs-descent', state)
                capture('smooth-stairs-descent.png')
                continue
            button('click')
            time.sleep(5)
            capture(f'smooth-{name}-before-check.png')
            command('state')
            state = command('arrived')
            report(name, state)
            if name == 'door':
                assert any(p['x'] == state['origin']['x'] + 3 and p['y'] == state['origin']['y'] for p in state['samples']), state
            capture(f'smooth-{name}.png')

    if not baseline and enabled('sustained-hold'):
        # Repeated cursor changes exercise the input/search hot path at 5 Hz.
        command('straight')
        time.sleep(.7)
        pid = os.environ['SANCTUARY_TEST_CLIENT_PID']
        def cpu_seconds():
            fields = Path(f'/proc/{pid}/stat').read_text().split(') ', 1)[1].split()
            return (int(fields[11]) + int(fields[12])) / os.sysconf('SC_CLK_TCK')
        idle_start, idle_time = cpu_seconds(), time.monotonic()
        time.sleep(2)
        idle_cpu_per_second = (cpu_seconds() - idle_start) / (time.monotonic() - idle_time)
        cpu_start, started = cpu_seconds(), time.monotonic()
        log_start = clientlog.stat().st_size
        mouse(430, 300)
        button('mousedown')
        try:
            for i in range(60):
                angle = i * math.pi / 15
                mouse(310 + 130 * math.cos(angle), 250 + 90 * math.sin(angle))
                time.sleep(.2)
        finally:
            button('mouseup')
        elapsed = time.monotonic() - started
        cpu_used = cpu_seconds() - cpu_start
        time.sleep(.5)
        command('mark')
        time.sleep(.6)
        state = command('stopped')
        import re
        durations = [float(v) for v in re.findall(r'Movement path: ms=([\d.]+)', clientlog.read_text(errors='replace')[log_start:])]
        assert durations and max(durations) < 100
        assert len(durations) / elapsed <= 6, (len(durations), elapsed)
        assert state['network']['PeakPacketRate'] <= 12, state['network']
        assert state['network']['QueueDepth'] == 0, state['network']
        assert {p['dir'] & 7 for p in state['samples']} == set(range(8)), state
        report('sustained-hold', state)
        reports['sustained-hold'].update({'wall_seconds': elapsed, 'process_cpu_seconds': cpu_used,
                                          'routes': len(durations), 'worst_search_ms': max(durations),
                                          'total_search_ms': sum(durations), 'idle_cpu_seconds_per_second': idle_cpu_per_second})
        print(f"Sustained hold: {elapsed:.1f}s, {len(durations)} routes, worst {max(durations):.2f}ms; CPU {cpu_used:.1f}s including software rendering; peak {state['network']['PeakPacketRate']} movement packets/s", flush=True)
    command('final')
    (ROOT / f'.local/logs/smooth-{label}.json').write_text(json.dumps(reports, indent=2))
