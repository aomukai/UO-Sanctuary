#!/usr/bin/env python3
"""Render the prepared client login screen in Xvfb without logging in."""
import argparse
import json
import os
from pathlib import Path
import subprocess
import sys
import time
from PIL import ImageGrab

ROOT = Path(__file__).resolve().parents[1]


def check(stage):
    sys.path.insert(0, str(ROOT / 'tools'))
    from prepare_class_skills_handoff import protected, selected_files, write, sha
    manifest = json.loads((stage / 'prepared-inputs.json').read_text())
    client = stage / 'client'
    assert not (stage / 'client-startup.json').exists(), 'Preserve prior evidence'
    assert os.environ.get('DISPLAY'), 'Run with xvfb-run'
    settings = json.loads((client / 'settings.json').read_text())
    settings.update(username='', password='', autologin=False, saveaccount=False,
                    reconnect=False, profilespath=str(stage / 'client-smoke-profiles'), plugins=[])
    config = stage / 'client-smoke-settings.json'
    write(config, settings)
    config.chmod(0o600)
    env = {**os.environ, 'DOTNET_ROOT': str(ROOT / '.tools/dotnet'),
           'LD_LIBRARY_PATH': str(client), 'SDL_AUDIODRIVER': 'dummy', 'LIBGL_ALWAYS_SOFTWARE': '1'}
    log = stage / 'client-startup.log'
    with log.open('w') as output:
        process = subprocess.Popen([str(ROOT / '.tools/dotnet/dotnet'), str(client / 'cuo.dll'),
                                    '-settings', str(config)], cwd=client, env=env,
                                   stdout=output, stderr=subprocess.STDOUT)
    try:
        deadline = time.monotonic() + 30
        window = None
        while time.monotonic() < deadline:
            assert process.poll() is None, 'Client exited before rendering; inspect client-startup.log'
            result = subprocess.run(['xdotool', 'search', '--onlyvisible', '--pid', str(process.pid)],
                                    capture_output=True, text=True)
            if result.returncode == 0:
                window = result.stdout.splitlines()[-1]
                break
            time.sleep(.2)
        assert window, 'Client did not create a visible window'
        time.sleep(3)
        assert process.poll() is None, 'Client exited after creating its window'
        geometry = subprocess.check_output(['xdotool', 'getwindowgeometry', '--shell', window], text=True)
        values = dict(line.split('=', 1) for line in geometry.splitlines())
        assert int(values['WIDTH']) >= 640 and int(values['HEIGHT']) >= 480
        screenshot = stage / 'client-login.png'
        ImageGrab.grab(xdisplay=os.environ['DISPLAY']).save(screenshot)
        startup = dict(windowWidth=int(values['WIDTH']), windowHeight=int(values['HEIGHT']),
                       aliveAfterRender=True, headless=True, loggedIn=False, gameplayVerified=False,
                       screenshotSha256=sha(screenshot), clientSha256=sha(client / 'cuo.dll'),
                       controlledStop='SIGTERM after startup capture')
    finally:
        if process.poll() is None:
            process.terminate()
            try:
                process.wait(timeout=10)
            except subprocess.TimeoutExpired:
                process.kill()
                process.wait()
    runtime = Path(manifest['runtimeRoot'])
    assert protected(runtime) == manifest['original']
    assert selected_files(runtime) == manifest['originalFiles']
    write(stage / 'client-startup.json', startup)
    print('Prepared client starts and renders in Xvfb; no login or original profile/config changes; gameplay remains separately qualified.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('stage', type=Path)
    check(parser.parse_args().stage.resolve())
