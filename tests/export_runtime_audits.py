#!/usr/bin/env python3
"""Export the real named/affix consumers from an explicitly selected isolated build."""
import argparse
import json
import os
from pathlib import Path
import shutil
import socket
import tempfile
from check_britain import ROOT, SERVER, Session


def export(stage):
    stage = stage.resolve(strict=True)
    native = Path(os.environ.get('SANCTUARY_NATIVE_ROOT', ROOT)) / 'upstream/ModernUO/Distribution'
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT), SANCTUARY_FIRST_LOOP_ROOT=str(ROOT),
                      SANCTUARY_NAMED_AUDIT=str(stage / 'named-item-runtime-audit.json'),
                      SANCTUARY_FIRST_LOOP_AUDIT=str(stage / 'first-loop-affix-audit.json'))
    with tempfile.TemporaryDirectory(prefix='sanctuary-runtime-audit-', dir='/dev/shm') as temporary:
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
        assemblies = json.loads((SERVER / 'Data/assemblies.json').read_text())
        for name, kind in [('Sanctuary.Content.dll', 'content'), ('Sanctuary.Integration.dll', 'integration')]:
            (folder / 'Assemblies' / name).symlink_to(stage / kind / name)
            if name not in assemblies:
                assemblies.append(name)
        (folder / 'Data/assemblies.json').write_text(json.dumps(assemblies))
        shutil.copytree(ROOT / 'data/Sanctuary', folder / 'Data/Sanctuary')
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
        with (stage / 'audit-native-checks.log').open('w') as log:
            session = Session(folder, log)
            try:
                ready = session.expect(r'create the owner account now\? \(y/n\):|Listening: 127.0.0.1:' + str(port))
                if ready.group(0).startswith('create'):
                    session.send('n'); session.expect('Listening: 127.0.0.1:' + str(port))
                for command in ['named-gear-check', 'first-loop-catalogue-check']:
                    session.send(command)
                    assert session.expect(command + r': (passed|failed)', timeout=120).group(1) == 'passed'
                session.send('shutdown'); assert session.process.wait(timeout=20) == 0
            finally:
                session.close()
        log = (stage / 'audit-native-checks.log').read_text()
        assert ' ERR]' not in log and ': failed' not in log
    print('Named and first-loop audits exported from the selected build in one disposable-world boot; installed world untouched.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--stage', type=Path, required=True)
    export(parser.parse_args().stage)
