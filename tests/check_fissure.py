#!/usr/bin/env python3
"""Prepared Fissure and retained Blizzard regression in a disposable world."""
import json
import hashlib
import os
from pathlib import Path
import shutil
import socket
import tempfile
import threading
from check_britain import ROOT, SERVER, Session

STAGE = ROOT / '.local/class-skills/fissure'


def drain(client):
    try:
        while client.recv(65536):
            pass
    except OSError:
        pass


def check():
    os.environ.setdefault('SANCTUARY_PROJECT_ROOT', str(ROOT))
    STAGE.mkdir(exist_ok=True)
    native = Path(os.environ.get('SANCTUARY_NATIVE_ROOT', str(ROOT))) / 'upstream/ModernUO/Distribution'
    with tempfile.TemporaryDirectory(prefix='sanctuary-fissure-', dir='/dev/shm') as temporary:
        folder = Path(temporary)
        for entry in SERVER.iterdir():
            if entry.is_file() and entry.suffix in ('.dll', '.json', '.config'):
                shutil.copy2(entry, folder / entry.name)
        (folder / 'runtimes').symlink_to(SERVER / 'runtimes', target_is_directory=True)
        shutil.copytree(SERVER / 'Configuration', folder / 'Configuration')
        for name in ['Assemblies', 'Data']:
            (folder / name).mkdir()
            for entry in (SERVER / name).iterdir():
                if entry.name not in ['Sanctuary.Content.dll', 'Sanctuary.Integration.dll', 'Sanctuary', 'assemblies.json']:
                    (folder / name / entry.name).symlink_to(entry, target_is_directory=entry.is_dir())
        for name in ['ModernUO.dll', 'Server.dll', 'Assemblies/UOContent.dll']:
            shutil.copy2(native / name, folder / name)
        for name, kind in [('Sanctuary.Content.dll', 'content'), ('Sanctuary.Integration.dll', 'integration')]:
            (folder / 'Assemblies' / name).symlink_to(STAGE / kind / name)
        shutil.copytree(ROOT / 'data/Sanctuary', folder / 'Data/Sanctuary')
        shutil.copy2(ROOT / '.local/class-skills/fissure-damage-source-probe.json', folder / 'fissure-damage-source-probe.json')
        assemblies = json.loads((SERVER / 'Data/assemblies.json').read_text())
        for name in ['Sanctuary.Content.dll', 'Sanctuary.Integration.dll']:
            if name not in assemblies:
                assemblies.append(name)
        (folder / 'Data/assemblies.json').write_text(json.dumps(assemblies))
        configpath = folder / 'Configuration/modernuo.json'
        config = json.loads(configpath.read_text())
        with socket.socket() as reservation:
            reservation.bind(('127.0.0.1', 0))
            port = reservation.getsockname()[1]
        with socket.socket(type=socket.SOCK_DGRAM) as reservation:
            reservation.bind(('127.0.0.1', 0))
            ping = reservation.getsockname()[1]
        config['listeners'] = [f'127.0.0.1:{port}']
        config['settings'].update({'autosave.enabled': 'False', 'pingServer.enabled': 'True', 'pingServer.port': str(ping),
                                   'sanctuary.graveyard.enabled': 'False', 'world.savePath': 'Saves',
                                   'crashGuard.restartServer': 'False', 'sanctuary.professions.enabled': 'True'})
        configpath.write_text(json.dumps(config))
        # Bind the recorded result to the exact copied data and tested DLLs.
        # This also distinguishes a later documentation-only status refresh.
        def digest(path):
            return hashlib.sha256(path.read_bytes()).hexdigest()
        sources = [p for directory in ['src/Sanctuary.Content', 'shared', 'tests/Sanctuary.Integration']
                   for p in (ROOT / directory).glob('*.cs')]
        sources += [Path(__file__).resolve(), ROOT / '.local/class-skills/fissure-damage-source-probe.json']
        inventory = dict(
            sourceInputs={str(p.relative_to(ROOT)): digest(p) for p in sorted(sources)},
            copiedData={str(p.relative_to(folder)): digest(p) for p in sorted((folder / 'Data/Sanctuary').rglob('*')) if p.is_file()},
            artifacts={str(p.relative_to(folder)): digest(p) for p in [folder / 'ModernUO.dll', folder / 'Server.dll',
                        folder / 'Assemblies/UOContent.dll', folder / 'Assemblies/Sanctuary.Content.dll', folder / 'Assemblies/Sanctuary.Integration.dll']})
        (STAGE / 'focused-inputs.json').write_text(json.dumps(inventory, indent=2) + '\n')
        logpath = STAGE / 'native-checks.log'
        try:
            with logpath.open('w') as log:
                for boot in range(3):
                    session = Session(folder, log)
                    try:
                        ready = session.expect(r'create the owner account now\? \(y/n\):|Listening: 127.0.0.1:' + str(port))
                        if ready.group(0).startswith('create'):
                            session.send('n')
                            session.expect(f'Listening: 127.0.0.1:{port}')
                        for family in ['fissure', 'blizzard']:
                            command = family + ('-check' if boot == 0 else '-reload-check')
                            session.send(command)
                            assert session.expect(command + r': (passed|failed)').group(1) == 'passed', logpath.read_text()[-6000:]
                            if family == 'blizzard':
                                result = 'blizzard-result' if boot == 0 else 'blizzard-reload-result'
                                assert session.expect(result + r': (passed|failed)', timeout=20).group(1) == 'passed', logpath.read_text()[-6000:]
                        with socket.create_connection(('127.0.0.1', port), timeout=5) as client:
                            threading.Thread(target=drain, args=(client,), daemon=True).start()
                            session.send('fissure-live-check')
                            assert session.expect(r'fissure-live-check: (passed|failed)').group(1) == 'passed'
                            assert session.expect(r'fissure-live-result: (passed|failed)', timeout=12).group(1) == 'passed', logpath.read_text()[-6000:]
                        session.send('save')
                        session.expect('Writing world save snapshot done')
                        session.send('shutdown')
                        assert session.process.wait(timeout=20) == 0
                    finally:
                        session.close()
            text = logpath.read_text()
            assert ' ERR]' not in text and ': failed' not in text
            shutil.copy2(folder / 'fissure-fixtures.json', STAGE / 'native-fixtures.json')
            print('Prepared Fissure and retained Blizzard pass3boots/2actual reloads/3connected natural actors; installed build untouched.')
        except BaseException:
            destination = STAGE / 'failed-world'
            destination.mkdir(exist_ok=True)
            for name in ['Saves', 'Configuration']:
                if (folder / name).exists():
                    shutil.copytree(folder / name, destination / name, dirs_exist_ok=True)
            for path in folder.glob('*.json'):
                shutil.copy2(path, destination / path.name)
            raise


if __name__ == '__main__':
    check()
