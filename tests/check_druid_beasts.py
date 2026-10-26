#!/usr/bin/env python3
"""Druid beast preparation and retained Creeper/summon consumers in a disposable world."""
import hashlib
import json
import os
from pathlib import Path
import shutil
import socket
import tempfile
from check_britain import ROOT, SERVER, Session

STAGE = ROOT / '.local/class-skills/beasts'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def check():
    os.environ.setdefault('SANCTUARY_PROJECT_ROOT', str(ROOT))
    native = Path(os.environ.get('SANCTUARY_NATIVE_ROOT', str(ROOT))) / 'upstream/ModernUO/Distribution'
    with tempfile.TemporaryDirectory(prefix='sanctuary-beast-stats-', dir='/dev/shm') as temporary:
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
        for name in ['skill-linear', 'creeper-vines', 'creeper-components', 'raven-components', 'raven-circle', 'toward-path', 'valkyrie-creation']:
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
        paths += [Path(__file__).resolve(), ROOT / '.local/class-skills/raven-components-source-probe.json', ROOT / '.local/class-skills/valkyrie-creation-source-probe.json']
        paths += [ROOT / ('.local/class-skills/' + name + '-source-probe.json') for name in ['skill-linear', 'creeper-vines', 'creeper-components', 'raven-circle', 'toward-path']]
        inventory = dict(sourceInputs={str(p.relative_to(ROOT)): sha(p) for p in sorted(paths)},
                         copiedData={str(p.relative_to(folder)): sha(p) for p in sorted((folder / 'Data/Sanctuary').rglob('*')) if p.is_file()},
                         artifacts={str(p.relative_to(folder)): sha(p) for p in [folder / 'ModernUO.dll', folder / 'Server.dll', folder / 'Assemblies/UOContent.dll', folder / 'Assemblies/Sanctuary.Content.dll', folder / 'Assemblies/Sanctuary.Integration.dll']})
        (STAGE / 'stats-inputs.json').write_text(json.dumps(inventory, indent=2) + '\n')
        logpath = STAGE / 'stats-native-checks.log'
        with logpath.open('w') as log:
            session = Session(folder, log)
            try:
                ready = session.expect(r'create the owner account now\? \(y/n\):|Listening: 127.0.0.1:' + str(port))
                if ready.group(0).startswith('create'):
                    session.send('n'); session.expect('Listening: 127.0.0.1:' + str(port))
                for command in ['skill-linear-check', 'creeper-stats-check', 'raven-stats-check', 'raven-flight-check', 'toward-path-check', 'valkyrie-creation-check', 'named-gear-check']:
                    session.send(command)
                    assert session.expect(command + r': (passed|failed)', timeout=120).group(1) == 'passed', logpath.read_text()[-5000:]
                session.send('shutdown'); assert session.process.wait(timeout=20) == 0
            finally:
                session.close()
        assert ' ERR]' not in logpath.read_text() and ': failed' not in logpath.read_text()
    print('Druid beast preparation and retained Creeper/Raven/flight/path/Valkyrie/item components pass one disposable boot; no new summon handler enabled; installed world untouched.')


if __name__ == '__main__':
    check()
