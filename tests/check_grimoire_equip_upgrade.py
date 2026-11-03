#!/usr/bin/env python3
"""Save accepted pre-equip Grimoire items; load/save their originals twice."""
import hashlib
import json
import os
from pathlib import Path
import shutil
import socket
import subprocess
import tempfile
from check_britain import ROOT, SERVER, Session

STAGE = ROOT / '.local/class-skills/grimoire-equip'
OLD = ROOT / '.local/class-skills/grimoire-capture'
PARENT = '4b0e3e09a20f37525f596108cf26ee85cc9165a6'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def check():
    receipt = subprocess.check_output(['git', 'show', PARENT + ':tools/reference/grimoire-capture-native-evidence.json'], cwd=ROOT)
    before = json.loads(receipt)['tested']
    with tempfile.TemporaryDirectory(prefix='sanctuary-grimoire-upgrade-', dir='/dev/shm') as temporary:
        folder = Path(temporary)
        for entry in SERVER.iterdir():
            if entry.is_file() and entry.suffix in ('.dll', '.json', '.config'):
                shutil.copy2(entry, folder / entry.name)
        (folder / 'runtimes').symlink_to(SERVER / 'runtimes', target_is_directory=True)
        shutil.copytree(SERVER / 'Configuration', folder / 'Configuration')
        for name in ('Assemblies', 'Data'):
            (folder / name).mkdir()
            for entry in (SERVER / name).iterdir():
                if entry.name not in ('Sanctuary.Content.dll', 'Sanctuary.Integration.dll', 'UOContent.dll', 'Sanctuary', 'assemblies.json'):
                    (folder / name / entry.name).symlink_to(entry, target_is_directory=entry.is_dir())
        for name in ('ModernUO.dll', 'Server.dll', 'Assemblies/UOContent.dll'):
            shutil.copy2(STAGE / 'native/Distribution' / name, folder / name)
            assert sha(folder / name) == before['artifacts'][name]
        assemblies = json.loads((SERVER / 'Data/assemblies.json').read_text())
        for name in ('Sanctuary.Content.dll', 'Sanctuary.Integration.dll'):
            if name not in assemblies:
                assemblies.append(name)
        (folder / 'Data/assemblies.json').write_text(json.dumps(assemblies))
        shutil.copytree(ROOT / 'data/Sanctuary', folder / 'Data/Sanctuary')
        sources = [p for directory in ('src/Sanctuary.Content', 'shared', 'tests/Sanctuary.Integration') for p in (ROOT / directory).glob('*.cs')]
        reference = ROOT / '.local/class-skills/warlock-hands-reference'
        for name, source in (('grimoire-armor-source.txt', 'armor.txt'), ('grimoire-automatic-source.txt', 'automagic.txt')):
            shutil.copy2(reference / source, folder / name)
            sources.append(reference / source)
        inputs = dict(sourceInputs={str(p.relative_to(ROOT)): sha(p) for p in sorted([*sources, Path(__file__).resolve()])},
                      artifacts={name: sha(folder / name) for name in ('ModernUO.dll', 'Server.dll', 'Assemblies/UOContent.dll')},
                      copiedData={str(p.relative_to(folder)): sha(p) for p in (folder / 'Data/Sanctuary').rglob('*') if p.is_file()},
                      oldArtifacts={}, oldReceipt=dict(commit=PARENT, sha256=hashlib.sha256(receipt).hexdigest()), boots=[])
        assert inputs['copiedData'] == before['copiedData']
        for kind in ('content', 'integration'):
            name = 'Sanctuary.' + kind.title() + '.dll'
            assert sha(OLD / kind / name) == before['artifacts']['Assemblies/' + name]
            inputs['oldArtifacts']['Assemblies/' + name] = sha(OLD / kind / name)
            inputs['artifacts']['Assemblies/' + name] = sha(STAGE / kind / name)
        configpath = folder / 'Configuration/modernuo.json'
        config = json.loads(configpath.read_text())
        with socket.socket() as reservation:
            reservation.bind(('127.0.0.1', 0)); port = reservation.getsockname()[1]
        with socket.socket(type=socket.SOCK_DGRAM) as reservation:
            reservation.bind(('127.0.0.1', 0)); ping = reservation.getsockname()[1]
        config['listeners'] = [f'127.0.0.1:{port}']
        config['settings'].update({'autosave.enabled': 'False', 'pingServer.enabled': 'True', 'pingServer.port': str(ping),
                                  'sanctuary.graveyard.enabled': 'False', 'world.savePath': 'Saves', 'crashGuard.restartServer': 'False', 'sanctuary.professions.enabled': 'True'})
        configpath.write_text(json.dumps(config))
        inventory = STAGE / 'upgrade-inputs.json'
        inventory.write_text(json.dumps(inputs, indent=2) + '\n')
        logpath = STAGE / 'upgrade-native-checks.log'
        with logpath.open('w') as log:
            for boot, command in enumerate(('grimoire-capture-check', 'grimoire-capture-upgrade-check', 'grimoire-capture-upgrade-reload-check')):
                package = OLD if boot == 0 else STAGE
                for kind in ('content', 'integration'):
                    name = 'Sanctuary.' + kind.title() + '.dll'
                    target = folder / 'Assemblies' / name
                    if target.is_symlink():
                        target.unlink()
                    target.symlink_to(package / kind / name)
                loaded = {name: sha(folder / name) for name in inputs['artifacts']}
                for name, digest in loaded.items():
                    assert digest == (inputs['oldArtifacts'][name] if boot == 0 and name.startswith('Assemblies/Sanctuary.') else inputs['artifacts'][name])
                session = Session(folder, log)
                try:
                    ready = session.expect(r'create the owner account now\? \(y/n\):|Listening: 127.0.0.1:' + str(port))
                    if ready.group(0).startswith('create'):
                        session.send('n'); session.expect('Listening: 127.0.0.1:' + str(port))
                    session.send(command)
                    assert session.expect(command + r': (passed|failed)').group(1) == 'passed', logpath.read_text()[-5000:]
                    session.send('save'); session.expect('Writing world save snapshot done')
                    shutil.copy2(folder / 'grimoire-capture-fixtures.json', STAGE / 'upgrade-grimoire-capture-fixtures.json')
                    session.send('shutdown'); assert session.process.wait(timeout=20) == 0
                    assert loaded == {name: sha(folder / name) for name in loaded}
                    inputs['boots'].append(dict(index=boot, artifacts=loaded,
                                                savedFiles={str(p.relative_to(folder)): sha(p) for p in sorted((folder / 'Saves').rglob('*')) if p.is_file()}))
                    inventory.write_text(json.dumps(inputs, indent=2) + '\n')
                finally:
                    session.close()
        log = logpath.read_text()
        assert ': failed' not in log and ' ERR]' not in log and log.count(': passed') == 3
        assert log.count('Shutting down') == log.count('Writing world save snapshot done') == 3
    print('Grimoire old-content upgrade:three boots/two actual reloads;accepted old content/integration creates originals,new build binds them without changing capture,identity,durability or owner resources.')


if __name__ == '__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT), SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check()
