#!/usr/bin/env python3
"""Boot accepted pre-resource content, then load/save its bound originals twice with the resource candidate."""
import hashlib
import json
import os
from pathlib import Path
import shutil
import socket
import subprocess
import tempfile
import threading
from check_britain import ROOT, SERVER, Session
from check_class_skills import drain_fixture_client

STAGE = ROOT / '.local/class-skills/npc-equipment-regen-basis'
OLD = Path(os.environ['SANCTUARY_NPC_BASIS_OLD_PACKAGE'])


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def check():
    native = STAGE / 'native/Distribution'
    with tempfile.TemporaryDirectory(prefix='sanctuary-npc-regen-basis-', dir='/dev/shm') as temporary:
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
            (folder / 'Assemblies' / name).symlink_to(STAGE / kind / name)
            if name not in assemblies:
                assemblies.append(name)
        (folder / 'Data/assemblies.json').write_text(json.dumps(assemblies))
        shutil.copytree(ROOT / 'data/Sanctuary', folder / 'Data/Sanctuary')
        sources = [p for directory in ['src/Sanctuary.Content', 'shared', 'tests/Sanctuary.Integration']
                   for p in (ROOT / directory).glob('*.cs')] + [Path(__file__).resolve()]
        inputs = dict(sourceInputs={str(p.relative_to(ROOT)):sha(p) for p in sorted(sources)},
            artifacts={name:sha(folder/name) for name in ['ModernUO.dll','Server.dll','Assemblies/UOContent.dll','Assemblies/Sanctuary.Content.dll','Assemblies/Sanctuary.Integration.dll']},
            copiedData={str(p.relative_to(folder)):sha(p) for p in (folder/'Data/Sanctuary').rglob('*') if p.is_file()})
        inputs['scope'] = 'new candidate source inputs; boot0 uses independently accepted old package, boots1/2 use the new candidate'
        inputs['oldArtifacts'] = {'Sanctuary.Content.dll':sha(OLD/'content/Sanctuary.Content.dll')}
        receipt = subprocess.check_output(['git','show','385fa98:tools/reference/npc-equipment-regeneration-evidence.json'],cwd=ROOT)
        before = json.loads(receipt)['inputInventory']
        assert inputs['oldArtifacts']['Sanctuary.Content.dll'] == before['artifacts']['Assemblies/Sanctuary.Content.dll']
        assert inputs['copiedData'] == before['copiedData']
        for name in ['ModernUO.dll','Server.dll','Assemblies/UOContent.dll']:
            assert inputs['artifacts'][name] == before['artifacts'][name]
        inputs['oldReceipt'] = dict(commit='385fa98',sha256=hashlib.sha256(receipt).hexdigest(),scope='Accepted old content with the new compatible integration assembly on every boot')
        inputs['boots'] = []
        (STAGE / 'upgrade-inputs.json').write_text(json.dumps(inputs, indent=2) + '\n')
        configpath = folder / 'Configuration/modernuo.json'
        config = json.loads(configpath.read_text())
        with socket.socket() as reservation:
            reservation.bind(('127.0.0.1', 0)); port = reservation.getsockname()[1]
        with socket.socket(type=socket.SOCK_DGRAM) as reservation:
            reservation.bind(('127.0.0.1', 0)); ping = reservation.getsockname()[1]
        config['listeners'] = [f'127.0.0.1:{port}']
        config['settings'].update({'autosave.enabled':'False','pingServer.enabled':'True','pingServer.port':str(ping),
            'sanctuary.graveyard.enabled':'False','world.savePath':'Saves','crashGuard.restartServer':'False','sanctuary.professions.enabled':'True'})
        configpath.write_text(json.dumps(config))
        logpath = STAGE / 'upgrade-native-checks.log'
        with logpath.open('w') as log:
            for boot in range(3):
                package = OLD if boot == 0 else STAGE
                for kind in ['content','integration']:
                    name = 'Sanctuary.' + kind.title() + '.dll'
                    target = folder / 'Assemblies' / name
                    target.unlink(); target.symlink_to((package if kind=='content' else STAGE) / kind / name)
                loaded = {name:sha(folder/name) for name in inputs['artifacts']}
                for name,value in loaded.items():
                    expected = inputs['oldArtifacts'][name.removeprefix('Assemblies/')] if boot == 0 and name=='Assemblies/Sanctuary.Content.dll' else inputs['artifacts'][name]
                    assert value == expected, (boot,name)
                session = Session(folder, log)
                try:
                    ready = session.expect(r'create the owner account now\? \(y/n\):|Listening: 127.0.0.1:' + str(port))
                    if ready.group(0).startswith('create'):
                        session.send('n'); session.expect('Listening: 127.0.0.1:' + str(port))
                    for command in [['npc-regen-basis-legacy-create-check'],['npc-regen-basis-legacy-upgrade-check'],['npc-regen-basis-legacy-reload-check']][boot]:
                        session.send(command)
                        assert session.expect(command + r': (passed|failed)').group(1) == 'passed', logpath.read_text()[-5000:]
                    session.send('save'); session.expect('Writing world save snapshot done')
                    for fixture in ['npc-regen-basis-legacy-fixtures.json']:
                        shutil.copy2(folder / fixture, STAGE / ('upgrade-' + fixture))
                    session.send('shutdown'); assert session.process.wait(timeout=20) == 0
                    assert loaded == {name:sha(folder/name) for name in loaded}
                    inputs['boots'].append(dict(index=boot,artifacts=loaded,
                        savedFiles={str(p.relative_to(folder)):sha(p) for p in sorted((folder/'Saves').rglob('*')) if p.is_file()}))
                    (STAGE/'upgrade-inputs.json').write_text(json.dumps(inputs,indent=2)+'\n')
                finally:
                    session.close()
    log = logpath.read_text()
    assert ': failed' not in log and ' ERR]' not in log and log.count(': passed') == 3
    assert log.count('Shutting down') == log.count('Writing world save snapshot done') == 3
    print('NPC regen basis legacy upgrade:3checks/3boots/2actual reloads;old omitted observation history preserves its saved rate;first verified max-life transitions update and persist the new exact basis without changing original rolls/balance.')


if __name__ == '__main__':
    check()
