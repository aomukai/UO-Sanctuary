#!/usr/bin/env python3
"""Prepared Spirit Wolf life/ownership plus retained summon consumers across actual save/reload cycles."""
import hashlib
import json
import os
from pathlib import Path
import shutil
import socket
import struct
import tempfile
import threading
import time
from check_britain import ROOT, SERVER, Session
from check_raven_stats import drain

STAGE = ROOT / '.local/class-skills/spirit-wolf'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def check():
    os.environ.setdefault('SANCTUARY_PROJECT_ROOT', str(ROOT))
    audit = bool(os.environ.get('SANCTUARY_FIRST_LOOP_AUDIT'))
    if audit:
        os.environ.setdefault('SANCTUARY_FIRST_LOOP_ROOT', str(ROOT))
        assert (Path(os.environ['SANCTUARY_FIRST_LOOP_ROOT']) / 'tools/reference/first-loop-cohort.json').is_file()
    native = Path(os.environ.get('SANCTUARY_NATIVE_ROOT', str(ROOT))) / 'upstream/ModernUO/Distribution'
    with tempfile.TemporaryDirectory(prefix='sanctuary-spirit-wolf-', dir='/dev/shm') as temporary:
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
        for name in ['pet-movement', 'spirit-wolf-rate', 'spirit-wolf-hit', 'spirit-wolf-corrected-ai', 'spirit-wolf-ai', 'spirit-wolf-creation', 'skill-linear', 'creeper-vines', 'creeper-components', 'raven-components', 'raven-circle', 'toward-path', 'valkyrie-creation', 'inner-sight']:
            shutil.copy2(ROOT / '.local/class-skills' / (name + '-source-probe.json'), folder / (name + '-source-probe.json'))
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
        paths += [Path(__file__).resolve(), ROOT / 'tests/check_raven_stats.py', ROOT / '.local/class-skills/raven-components-source-probe.json', ROOT / '.local/class-skills/valkyrie-creation-source-probe.json']
        paths += [ROOT / ('.local/class-skills/' + name + '-source-probe.json') for name in ['pet-movement', 'spirit-wolf-rate', 'spirit-wolf-hit', 'spirit-wolf-corrected-ai', 'spirit-wolf-ai', 'spirit-wolf-creation', 'skill-linear', 'creeper-vines', 'creeper-components', 'raven-circle', 'toward-path', 'inner-sight', 'battle-orders']]
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
                    commands = ['pet-movement-check', 'skill-linear-check', 'spirit-wolf-stats-check', 'spirit-wolf-hit-check', 'spirit-wolf-rate-check', 'creeper-stats-check', 'raven-stats-check', 'raven-flight-check', 'toward-path-check', 'valkyrie-creation-check', 'named-gear-check'] if boot == 0 else []
                    commands += [name + ('-check' if boot == 0 else '-reload-check') for name in ['spirit-wolf-casting', 'spirit-wolf-world', 'spirit-wolf-body', 'battle-orders', 'shout', 'summon-difficulty', 'class-history', 'full-wardrobe', 'inner-sight']]
                    for command in commands:
                        session.send(command)
                        assert session.expect(command + r': (passed|failed)', timeout=120).group(1) == 'passed', logpath.read_text()[-5000:]
                        family = command.removesuffix('-reload-check').removesuffix('-check')
                        if family in ['battle-orders', 'shout'] or boot == 0 and family == 'inner-sight':
                            assert session.expect(family + r'-result: (passed|failed)', timeout=30).group(1) == 'passed', logpath.read_text()[-5000:]
                    command = 'decoy-check' if boot == 0 else 'decoy-reload-check'
                    with socket.create_connection(('127.0.0.1', port), timeout=5) as client:
                        threading.Thread(target=drain, args=(client,), daemon=True).start()
                        time.sleep(.15)
                        session.send(command)
                        assert session.expect(command + r': (passed|failed)').group(1) == 'passed', logpath.read_text()[-5000:]
                        ready = session.expect(r'decoy-input: ready ability=(\d+) x=(\d+) y=(\d+) z=(-?\d+)')
                        ability, x, y, z = map(int, ready.groups())
                        client.sendall(struct.pack('>BHHBBIBIHHb', 0xBF, 21, 0xF2, 7, 3, 1, ability, 0, x, y, z))
                        assert session.expect(r'decoy-result: (passed|failed)', timeout=20).group(1) == 'passed', logpath.read_text()[-5000:]
                        client.shutdown(socket.SHUT_RDWR)
                    time.sleep(.2)
                    session.send('decoy-disconnect-check')
                    assert session.expect(r'decoy-disconnect-check: (passed|failed)').group(1) == 'passed', logpath.read_text()[-5000:]
                    if boot == 0 and audit:
                        session.send('first-loop-catalogue-check')
                        assert session.expect(r'first-loop-catalogue-check: (passed|failed)', timeout=120).group(1) == 'passed', logpath.read_text()[-5000:]
                    for family in ['spirit-wolf-world-live', 'spirit-wolf-hit-live', 'spirit-wolf-rate-live', 'spirit-wolf-slow-live']:
                        with socket.create_connection(('127.0.0.1', port), timeout=5) as client:
                            threading.Thread(target=drain, args=(client,), daemon=True).start()
                            session.send(family + '-check')
                            assert session.expect(family + r'-check: (passed|failed)').group(1) == 'passed', logpath.read_text()[-5000:]
                            session.expect(family + ': ready')
                            client.sendall(b'\x73\x00')
                            assert session.expect(family + r'-result: (passed|failed)', timeout=25 if family=='spirit-wolf-slow-live' else 15).group(1) == 'passed', logpath.read_text()[-5000:]
                    for family in ['spirit-wolf-right', 'spirit-wolf-quick']:
                        with socket.create_connection(('127.0.0.1', port), timeout=5) as client:
                            threading.Thread(target=drain, args=(client,), daemon=True).start()
                            session.send(family + '-check')
                            assert session.expect(family + r'-check: (passed|failed)').group(1) == 'passed'
                            ready = session.expect(family + r': ready ability=(\d+) class=(\d+) x=(\d+) y=(\d+) z=(-?\d+)')
                            ability, character, x, y, z = map(int, ready.groups())
                            if family == 'spirit-wolf-right':
                                client.sendall(struct.pack('>BHHBBIBIHHb', 0xBF, 21, 0xF2, 7, 4, 1, ability, 0, x, y, z))
                            else:
                                client.sendall(struct.pack('>BHHBBIBBBIHHb', 0xBF, 23, 0xF7, 2, 4, 1, character, 7, ability, 0, x, y, z))
                            session.expect(family + ': windup')
                            if family == 'spirit-wolf-right':
                                client.sendall(struct.pack('>BHHBBI', 0xBF, 11, 0xF2, 7, 5, 2))
                            assert session.expect(family + r'-result: (passed|failed)', timeout=10).group(1) == 'passed', logpath.read_text()[-5000:]
                    session.send('save'); session.expect('Writing world save snapshot done')
                    session.send('shutdown'); assert session.process.wait(timeout=20) == 0
                finally:
                    session.close()
        shutil.copy2(folder / 'spirit-wolf-casting-fixtures.json', STAGE / 'casting-fixtures.json')
        shutil.copy2(folder / 'spirit-wolf-body-fixtures.json', STAGE / 'body-fixtures.json')
        shutil.copy2(folder / 'spirit-wolf-world-fixtures.json', STAGE / 'world-fixtures.json')
        assert ' ERR]' not in logpath.read_text() and ': failed' not in logpath.read_text()
    print('Prepared Spirit Wolf life/ownership/world movement/cold melee/rate and retained consumers pass3boots/2actual reloads/3natural follows/3natural attacks/3natural haste attacks/3natural repeated slow attacks; exact busy cadence and physical floor provisional, learned casting and3right/3quick socket casts pass; installed world untouched.')


if __name__ == '__main__':
    check()
