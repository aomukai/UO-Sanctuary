#!/usr/bin/env python3
"""Verify named gear across two actual reloads using an explicit candidate build."""
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
                      SANCTUARY_NAMED_AUDIT=str(stage / 'full-named-item-runtime-audit.json'),
                      SANCTUARY_FIRST_LOOP_AUDIT=str(stage / 'first-loop-affix-audit.json'))
    with tempfile.TemporaryDirectory(prefix='sanctuary-named-candidate-', dir='/dev/shm') as temporary:
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
        import hashlib
        def digest(path):return hashlib.sha256(path.read_bytes()).hexdigest()
        actual=dict(artifacts={name:digest(folder/name) for name in ['ModernUO.dll','Server.dll','Assemblies/UOContent.dll','Assemblies/Sanctuary.Content.dll','Assemblies/Sanctuary.Integration.dll']},
                    copiedData={str(p.relative_to(folder)):digest(p) for p in (folder/'Data/Sanctuary').rglob('*') if p.is_file()})
        full=json.loads((stage/'class-inputs.json').read_text())
        assert actual['artifacts']==full['artifacts'] and actual['copiedData']==full['copiedData']
        (stage/'named-inputs.json').write_text(json.dumps(actual,indent=2)+'\n')
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
        with (stage / 'named-native-checks.log').open('w') as log:
          for boot in range(3):
            session = Session(folder, log)
            try:
                ready = session.expect(r'create the owner account now\? \(y/n\):|Listening: 127.0.0.1:' + str(port))
                if ready.group(0).startswith('create'):
                    session.send('n'); session.expect('Listening: 127.0.0.1:' + str(port))
                for command in ['named-gear-check' if boot == 0 else 'named-gear-reload-check']:
                    session.send(command)
                    assert session.expect(command + r': (passed|failed)', timeout=120).group(1) == 'passed'
                session.send('save'); session.expect('Writing world save snapshot done')
                session.send('shutdown'); assert session.process.wait(timeout=20) == 0
            finally:
                session.close()
        log = (stage / 'named-native-checks.log').read_text()
        assert ' ERR]' not in log and ': failed' not in log
    assert log.count(': passed') == log.count('Shutting down') == log.count('Writing world save snapshot done') == 3
    print('Named candidate:3markers/3boots/2actual reloads; identical explicit candidate, original world untouched.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--stage', type=Path, required=True)
    export(parser.parse_args().stage)
