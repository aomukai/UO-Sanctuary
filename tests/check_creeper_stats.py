#!/usr/bin/env python3
"""Prepared Creeper arithmetic/outer AI and retained summon consumers in a disposable world."""
import hashlib
import json
import os
from pathlib import Path
import shutil
import socket
import struct
import threading
import time
from check_raven_stats import drain
import tempfile
from check_britain import ROOT, SERVER, Session

STAGE = ROOT / '.local/class-skills/creeper'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def check(extra_commands=(), extra_probes=(), extra_reload_commands=(), extra_fixtures=(), extra_live_commands=(), extra_before_save_commands=(), extra_input_commands=(), extra_probe_files=(), extra_target_input_commands=(), extra_wait_results=None, between_boot_delay=0, extra_reconnect_commands=(), readyless_live_commands=()):
    assert 0 <= between_boot_delay <= 60
    os.environ.setdefault('SANCTUARY_PROJECT_ROOT', str(ROOT))
    audit = bool(os.environ.get('SANCTUARY_FIRST_LOOP_AUDIT'))
    if audit:
        os.environ.setdefault('SANCTUARY_FIRST_LOOP_ROOT', str(ROOT))
    native = Path(os.environ.get('SANCTUARY_NATIVE_ROOT', str(ROOT))) / 'upstream/ModernUO/Distribution'
    with tempfile.TemporaryDirectory(prefix='sanctuary-creeper-stats-', dir='/dev/shm') as temporary:
        folder = Path(temporary)
        for entry in SERVER.iterdir():
            if entry.is_file() and entry.suffix in ['.dll', '.json', '.config']:
                shutil.copy2(entry, folder / entry.name)
        (folder / 'runtimes').symlink_to(SERVER / 'runtimes', target_is_directory=True)
        shutil.copytree(SERVER / 'Configuration', folder / 'Configuration')
        for name in ['Assemblies', 'Data']:
            (folder / name).mkdir()
            for entry in (SERVER / name).iterdir():
                if entry.name not in ['Sanctuary.Content.dll', 'Sanctuary.Integration.dll', 'UOContent.dll', 'Sanctuary', 'assemblies.json']:
                    (folder / name / entry.name).symlink_to(entry, target_is_directory=entry.is_dir())
        for name in ['ModernUO.dll', 'Server.dll', 'Assemblies/UOContent.dll']:
            shutil.copy2(native / name, folder / name)
        for name, kind in [('Sanctuary.Content.dll', 'content'), ('Sanctuary.Integration.dll', 'integration')]:
            (folder / 'Assemblies' / name).symlink_to(STAGE / kind / name)
        shutil.copytree(ROOT / 'data/Sanctuary', folder / 'Data/Sanctuary')
        for name in ['creeper-ai', 'creeper-vines-age', 'creeper-components', 'creeper-frame', 'raven-components', 'raven-circle', 'toward-path', 'valkyrie-creation', *extra_probes]:
            shutil.copy2(ROOT / '.local/class-skills' / (name + '-source-probe.json'), folder / (name + '-source-probe.json'))
        for name in extra_probe_files:
            shutil.copy2(ROOT / '.local/class-skills' / name, folder / name)
        assemblies = json.loads((SERVER / 'Data/assemblies.json').read_text())
        for name in ['Sanctuary.Content.dll', 'Sanctuary.Integration.dll']:
            if name not in assemblies:
                assemblies.append(name)
        (folder / 'Data/assemblies.json').write_text(json.dumps(assemblies))
        configpath = folder / 'Configuration/modernuo.json'
        config = json.loads(configpath.read_text())
        with socket.socket() as reservation:
            reservation.bind(('127.0.0.1', 0)); port = reservation.getsockname()[1]
        with socket.socket(type=socket.SOCK_DGRAM) as reservation:
            reservation.bind(('127.0.0.1', 0)); ping = reservation.getsockname()[1]
        config['listeners'] = [f'127.0.0.1:{port}']
        config['settings'].update({'autosave.enabled': 'False', 'pingServer.enabled': 'True', 'pingServer.port': str(ping),
                                  'sanctuary.graveyard.enabled': 'False', 'world.savePath': 'Saves', 'crashGuard.restartServer': 'False'})
        configpath.write_text(json.dumps(config))
        paths = [p for directory in ['src/Sanctuary.Content', 'shared', 'tests/Sanctuary.Integration'] for p in (ROOT / directory).glob('*.cs')]
        paths += [Path(__file__).resolve(), ROOT / '.local/class-skills/raven-components-source-probe.json', ROOT / '.local/class-skills/valkyrie-creation-source-probe.json']
        paths += [ROOT / ('.local/class-skills/' + name + '-source-probe.json') for name in ['creeper-ai', 'creeper-vines-age', 'creeper-components', 'creeper-frame', 'raven-circle', 'toward-path']]
        paths += [ROOT / ('.local/class-skills/' + name + '-source-probe.json') for name in extra_probes]
        paths += [ROOT / '.local/class-skills' / name for name in extra_probe_files]
        inventory = dict(sourceInputs={str(p.relative_to(ROOT)): sha(p) for p in sorted(paths)},
                         copiedData={str(p.relative_to(folder)): sha(p) for p in sorted((folder / 'Data/Sanctuary').rglob('*')) if p.is_file()},
                         artifacts={str(p.relative_to(folder)): sha(p) for p in [folder / 'ModernUO.dll', folder / 'Server.dll', folder / 'Assemblies/UOContent.dll', folder / 'Assemblies/Sanctuary.Content.dll', folder / 'Assemblies/Sanctuary.Integration.dll']})
        (STAGE / 'stats-inputs.json').write_text(json.dumps(inventory, indent=2) + '\n')
        logpath = STAGE / 'stats-native-checks.log'
        with logpath.open('w') as log:
            for boot in range(3):
                session = Session(folder, log)
                try:
                    ready = session.expect(r'create the owner account now\? \(y/n\):|Listening: 127.0.0.1:' + str(port))
                    if ready.group(0).startswith('create'):
                        session.send('n'); session.expect('Listening: 127.0.0.1:' + str(port))
                    commands = ['creeper-ai-check', 'creeper-stats-check', 'creeper-fields-check', 'raven-stats-check', 'raven-flight-check', 'toward-path-check', 'valkyrie-creation-check', 'named-gear-check'] if boot == 0 else []
                    if boot == 0:
                        commands[:0] = extra_commands
                    else:
                        commands[:0] = extra_reload_commands
                    commands.append('spirit-wolf-world-check' if boot == 0 else 'spirit-wolf-world-reload-check')
                    commands.append('creeper-body-check' if boot == 0 else 'creeper-body-reload-check')
                    commands.append('creeper-world-check' if boot == 0 else 'creeper-world-reload-check')
                    commands.append('creeper-ground-check' if boot == 0 else 'creeper-ground-reload-check')
                    commands.append('creeper-poison-check' if boot == 0 else 'creeper-poison-reload-check')
                    commands.append('creeper-casting-check' if boot == 0 else 'creeper-casting-reload-check')
                    if audit and boot == 0:
                        commands.append('first-loop-catalogue-check')
                    for command in commands:
                        session.send(command)
                        assert session.expect(command + r': (passed|failed)', timeout=120).group(1) == 'passed', logpath.read_text()[-5000:]
                        if extra_wait_results and command in extra_wait_results:
                            result = extra_wait_results[command]
                            assert session.expect(result + r': (passed|failed)', timeout=120).group(1) == 'passed', logpath.read_text()[-5000:]
                    with socket.create_connection(('127.0.0.1', port), timeout=5) as client:
                        threading.Thread(target=drain, args=(client,), daemon=True).start()
                        session.send('creeper-world-live-check')
                        assert session.expect(r'creeper-world-live-check: (passed|failed)').group(1) == 'passed'
                        session.expect('creeper-world-live: ready')
                        client.sendall(b'\x73\x00')
                        assert session.expect(r'creeper-world-live-result: (passed|failed)', timeout=15).group(1) == 'passed', logpath.read_text()[-5000:]
                    for live in extra_live_commands:
                        with socket.create_connection(('127.0.0.1', port), timeout=5) as client:
                            threading.Thread(target=drain, args=(client,), daemon=True).start()
                            session.send(live + '-check')
                            assert session.expect(live + r'-check: (passed|failed)').group(1) == 'passed'
                            if live not in readyless_live_commands:
                                session.expect(live + ': ready')
                                client.sendall(b'\x73\x00')
                            assert session.expect(live + r'-result: (passed|failed)', timeout=25 if live=='spirit-wolf-slow-live' else 15).group(1) == 'passed', logpath.read_text()[-5000:]
                    for family in extra_reconnect_commands:
                        with socket.create_connection(('127.0.0.1', port), timeout=5) as client:
                            threading.Thread(target=drain, args=(client,), daemon=True).start()
                            session.send(family + '-check')
                            assert session.expect(family + r'-check: (passed|failed)').group(1) == 'passed'
                            session.expect(family + ': disconnect-ready')
                            client.shutdown(socket.SHUT_RDWR)
                        session.expect(family + ': disconnected', timeout=15)
                        with socket.create_connection(('127.0.0.1', port), timeout=5) as client:
                            threading.Thread(target=drain, args=(client,), daemon=True).start()
                            session.send(family + '-reconnect-check')
                            # A synchronous reconnect can finish before the console
                            # wrapper acknowledges it; an async one does the reverse.
                            received = set()
                            for _ in range(2):
                                marker = session.expect(family + r'-(reconnect-check|result): (passed|failed)', timeout=15)
                                assert marker.group(2) == 'passed' and marker.group(1) not in received, logpath.read_text()[-5000:]
                                received.add(marker.group(1))
                            assert received == {'reconnect-check', 'result'}
                    for family in extra_input_commands:
                        with socket.create_connection(('127.0.0.1', port), timeout=5) as client:
                            threading.Thread(target=drain, args=(client,), daemon=True).start()
                            session.send(family + '-check')
                            assert session.expect(family + r'-check: (passed|failed)').group(1) == 'passed'
                            ready = session.expect(family + r': ready ability=(\d+) class=(\d+) x=(\d+) y=(\d+) z=(-?\d+)')
                            ability, character, x, y, z = map(int, ready.groups())
                            if family.endswith('right'):
                                client.sendall(struct.pack('>BHHBBIBIHHb', 0xBF, 21, 0xF2, 7, 4, 1, ability, 0, x, y, z))
                            else:
                                client.sendall(struct.pack('>BHHBBIBBBIHHb', 0xBF, 23, 0xF7, 2, 4, 1, character, 7, ability, 0, x, y, z))
                            session.expect(family + ': windup')
                            if family.endswith('right'):
                                client.sendall(struct.pack('>BHHBBI', 0xBF, 11, 0xF2, 7, 5, 2))
                            assert session.expect(family + r'-result: (passed|failed)', timeout=10).group(1) == 'passed', logpath.read_text()[-5000:]
                    for family in extra_target_input_commands:
                        with socket.create_connection(('127.0.0.1', port), timeout=5) as client:
                            threading.Thread(target=drain, args=(client,), daemon=True).start()
                            session.send(family + '-check')
                            assert session.expect(family + r'-check: (passed|failed)').group(1) == 'passed'
                            ready = session.expect(family + r': ready ability=(\d+) class=(\d+) target=(\d+) x=(\d+) y=(\d+) z=(-?\d+)')
                            ability, character, target, x, y, z = map(int, ready.groups())
                            if family.endswith('right'):
                                client.sendall(struct.pack('>BHHBBIBIHHb', 0xBF, 21, 0xF2, 7, 4, 1, ability, target, x, y, z))
                            else:
                                client.sendall(struct.pack('>BHHBBIBBBIHHb', 0xBF, 23, 0xF7, 2, 4, 1, character, 7, ability, target, x, y, z))
                            session.expect(family + ': windup')
                            if family.endswith('right'):
                                client.sendall(struct.pack('>BHHBBI', 0xBF, 11, 0xF2, 7, 5, 2))
                            assert session.expect(family + r'-result: (passed|failed)', timeout=10).group(1) == 'passed', logpath.read_text()[-5000:]
                    if boot < 2:
                        for command in extra_before_save_commands:
                            session.send(command)
                            assert session.expect(command + r': (passed|failed)').group(1) == 'passed', logpath.read_text()[-5000:]
                    session.send('save'); session.expect('Writing world save snapshot done')
                    session.send('shutdown'); assert session.process.wait(timeout=20) == 0
                finally:
                    session.close()
                if boot < 2 and between_boot_delay:
                    time.sleep(between_boot_delay)
        shutil.copy2(folder / 'spirit-wolf-world-fixtures.json', STAGE / 'wolf-world-fixtures.json')
        shutil.copy2(folder / 'creeper-world-fixtures.json', STAGE / 'world-fixtures.json')
        shutil.copy2(folder / 'creeper-ground-fixtures.json', STAGE / 'ground-fixtures.json')
        shutil.copy2(folder / 'creeper-poison-fixtures.json', STAGE / 'poison-fixtures.json')
        shutil.copy2(folder / 'creeper-casting-fixtures.json', STAGE / 'casting-fixtures.json')
        shutil.copy2(folder / 'creeper-body-fixtures.json', STAGE / 'body-fixtures.json')
        for name in extra_fixtures:
            shutil.copy2(folder / name, STAGE / name)
        assert ' ERR]' not in logpath.read_text() and ': failed' not in logpath.read_text()
    print('Prepared Creeper arithmetic/outer AI and retained Raven/flight/path/Valkyrie/item components pass3boots/2actual reloads; saved life-only Creeper body checked, world AI/mats and learned handler pending; installed world untouched.')


if __name__ == '__main__':
    check()
