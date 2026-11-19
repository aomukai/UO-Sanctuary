#!/usr/bin/env python3
"""Render real F1/F2 Necromancer input and natural pet combat in a disposable world."""
import json
import os
from pathlib import Path
import shutil
import socket
import subprocess
import sys
import time

from check_britain import Session
from check_class_roster_ui import preserved_world

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/necromancer-rendered'
RUNTIME = Path('/media/aomukai/FILES/Sanctuary')


def exercise(window, click, capture, character, wait_for):
    # The existing creation helper exposes its specialised scene callback through
    # check_class_names_ui. Replace only that callback in this disposable test
    # process; leave the retained helper sources and their old evidence intact.
    click(210, 260)  # Necromancer, wire class 103.
    click(320, 82)
    subprocess.run(['xdotool', 'key', '--window', window, 'ctrl+a'], check=True)
    subprocess.run(['xdotool', 'type', '--window', window, character], check=True)
    click(615, 455)
    wait_for('Login step: CharacterCreationDone')
    time.sleep(1)

    def command(suffix):
        name = 'necromancer-visual-' + suffix
        log = Path(os.environ['SANCTUARY_TEST_LOG'])
        offset = log.stat().st_size
        os.write(int(os.environ['SANCTUARY_TEST_CONSOLE_FD']), (name + '\n').encode())
        deadline = time.monotonic() + 10
        while time.monotonic() < deadline:
            text = log.read_text(errors='replace')[offset:]
            if name + ': failed' in text:
                raise AssertionError(text[-3500:])
            if name + ': passed' in text:
                return json.loads(Path(os.environ['SANCTUARY_TEST_UI_STATE']).read_text())
            time.sleep(.1)
        raise AssertionError('Missing scene result: ' + name)

    def cast(state, name, key):
        player, target = state['player'], state[name]
        point = (310 + 22 * (target['x'] - player['x'] - target['y'] + player['y']),
                 250 + 22 * (target['x'] - player['x'] + target['y'] - player['y'])
                 - 4 * (target['z'] - player['z']))
        subprocess.run(['xdotool', 'mousemove', '--window', window, *map(str, point)], check=True)
        time.sleep(.2)
        subprocess.run(['xdotool', 'key', '--window', window, key], check=True)

    state = command('setup')
    time.sleep(1)
    # Use the existing combat-UI driver's verified paperdoll minimize control;
    # right-clicking its character artwork can select the mobile instead.
    click(538, 101)
    time.sleep(.4)
    capture('necromancer-before-summon.png')
    cast(state, 'corpse', 'F1')
    time.sleep(1.5)
    capture('necromancer-raised-warrior.png')
    state = command('raised')
    time.sleep(.5)
    cast(state, 'target', 'F2')
    time.sleep(.5)
    capture('necromancer-teeth-flight.png')
    time.sleep(1.5)
    command('teeth')
    capture('necromancer-teeth-impact.png')
    command('fight')
    time.sleep(1)
    capture('necromancer-warrior-approach.png')
    time.sleep(4)
    command('fought')
    capture('necromancer-warrior-combat.png')
    print('Rendered Necromancer:F1 learned Warrior,F2 isolated Teeth damage and original Warrior natural combat passed.', flush=True)


def check():
    run = STAGE / ('run-' + os.environ.get('SANCTUARY_VISUAL_RUN', '1'))
    run.mkdir()  # Preserve failed attempts; never overwrite an earlier run.
    server = RUNTIME / '.local/server'
    with preserved_world(run) as folder:
        for entry in server.iterdir():
            if entry.is_file() and entry.suffix in ('.dll', '.json', '.config'):
                shutil.copy2(entry, folder / entry.name)
        (folder / 'runtimes').symlink_to(server / 'runtimes', target_is_directory=True)
        shutil.copytree(server / 'Configuration', folder / 'Configuration')
        for name in ('Assemblies', 'Data'):
            (folder / name).mkdir()
            for entry in (server / name).iterdir():
                if entry.name not in ('Sanctuary.Content.dll', 'Sanctuary.Integration.dll', 'Sanctuary', 'assemblies.json'):
                    (folder / name / entry.name).symlink_to(entry, target_is_directory=entry.is_dir())
        (folder / 'Assemblies/Sanctuary.Content.dll').symlink_to(RUNTIME / '.local/content/Sanctuary.Content.dll')
        (folder / 'Assemblies/Sanctuary.Integration.dll').symlink_to((STAGE / 'integration-9/Sanctuary.Integration.dll').resolve())
        shutil.copytree(ROOT / 'data/Sanctuary', folder / 'Data/Sanctuary')
        manifest = json.loads((server / 'Data/assemblies.json').read_text())
        assert 'Sanctuary.Integration.dll' not in manifest
        (folder / 'Data/assemblies.json').write_text(json.dumps(manifest + ['Sanctuary.Integration.dll']))
        with socket.socket() as reservation:
            reservation.bind(('127.0.0.1', 0)); port = reservation.getsockname()[1]
        with socket.socket(type=socket.SOCK_DGRAM) as reservation:
            reservation.bind(('127.0.0.1', 0)); ping = reservation.getsockname()[1]
        config_path = folder / 'Configuration/modernuo.json'
        config = json.loads(config_path.read_text())
        config['listeners'] = [f'127.0.0.1:{port}']
        config['settings'].update({'autosave.enabled': 'False', 'pingServer.enabled': 'True',
            'pingServer.port': str(ping), 'world.savePath': 'Saves', 'crashGuard.restartServer': 'False',
            'sanctuary.graveyard.enabled': 'False', 'sanctuary.professions.enabled': 'False',
            'accountHandler.enableAutoAccountCreation': 'True', 'accountHandler.maxAccountsPerIP': '50'})
        config_path.write_text(json.dumps(config))
        os.environ.update(SANCTUARY_TEST_RUNTIME_ROOT=str(RUNTIME), SANCTUARY_TEST_LOG_DIR=str(run),
            SANCTUARY_TEST_LOG=str(run / 'server.log'), SANCTUARY_TEST_UI_STATE=str(run / 'scene.json'),
            SANCTUARY_TEST_CLIENT_DLL=str(RUNTIME / '.local/client/cuo.dll'), SANCTUARY_TEST_CLASS_NAMES_UI='1')
        with (run / 'server.log').open('w') as log:
            session = Session(folder, log)
            try:
                session.expect(r'create the owner account now\? \(y/n\):')
                session.send('n')
                session.expect(f'Listening: 127.0.0.1:{port}')
                env = {**os.environ, 'SANCTUARY_TEST_CONSOLE_FD': str(session.master)}
                with (run / 'ui.log').open('w') as output:
                    subprocess.run(['xvfb-run', '-a', 'python3', '-u', str(Path(__file__).resolve()), '--client', str(port)],
                        env=env, pass_fds=[session.master], stdout=output, stderr=subprocess.STDOUT,
                        check=True, timeout=120)
                session.send('save')
                session.expect('Writing world save snapshot done')
                session.send('shutdown')
                assert session.process.wait(timeout=20) == 0
            finally:
                session.close()
    (run / 'result.json').write_text(json.dumps({'wireClass': 103, 'realClient': True,
        'f1Raise': True, 'f2TeethDamage': True, 'naturalOriginalWarriorDamage': True,
        'finalAssets': False, 'allNecromancerTalentsRendered': False}, indent=2) + '\n')


if __name__ == '__main__':
    if len(sys.argv) > 1 and sys.argv[1] == '--client':
        import check_creation_ui
        import check_class_names_ui
        check_class_names_ui.exercise = exercise
        check_creation_ui.check(sys.argv[2])
    else:
        check()
