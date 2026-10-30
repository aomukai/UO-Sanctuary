#!/usr/bin/env python3
"""Verify non-armor defense through actual equipment, source graph and saved readers."""
import hashlib
import json
import os
from pathlib import Path
import shutil
import socket
import tempfile
from check_britain import ROOT, SERVER, Session

STAGE = ROOT / '.local/class-skills/npc-equipment-defense'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def check():
    native = STAGE / 'native/Distribution'
    with tempfile.TemporaryDirectory(prefix='sanctuary-npc-equipment-defense-', dir='/dev/shm') as temporary:
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
        shutil.copy2(ROOT/'.local/class-skills/npc-equipment-defense-source/cases.json',folder/'npc-equipment-defense-source-cases.json')
        sources = [p for directory in ['src/Sanctuary.Content', 'shared', 'tests/Sanctuary.Integration']
                   for p in (ROOT / directory).glob('*.cs')] + [Path(__file__).resolve()]
        inputs = dict(sourceInputs={str(p.relative_to(ROOT)):sha(p) for p in sorted(sources)},
            artifacts={name:sha(folder/name) for name in ['ModernUO.dll','Server.dll','Assemblies/UOContent.dll','Assemblies/Sanctuary.Content.dll','Assemblies/Sanctuary.Integration.dll']},
            copiedData={str(p.relative_to(folder)):sha(p) for p in (folder/'Data/Sanctuary').rglob('*') if p.is_file()})
        (STAGE / 'defense-inputs.json').write_text(json.dumps(inputs, indent=2) + '\n')
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
        logpath = STAGE / 'defense-native-checks.log'
        with logpath.open('w') as log:
            for boot in range(3):
                session = Session(folder, log)
                try:
                    ready = session.expect(r'create the owner account now\? \(y/n\):|Listening: 127.0.0.1:' + str(port))
                    if ready.group(0).startswith('create'):
                        session.send('n'); session.expect('Listening: 127.0.0.1:' + str(port))
                    for family in ['npc-equipment-defense']:
                        command = family + ('-check' if boot == 0 else '-reload-check')
                        session.send(command)
                        assert session.expect(command + r': (passed|failed)').group(1) == 'passed', logpath.read_text()[-5000:]
                    session.send('save'); session.expect('Writing world save snapshot done')
                    for fixture in ['npc-equipment-defense-fixtures.json']:
                        shutil.copy2(folder / fixture, STAGE / ('isolated-' + fixture))
                    session.send('shutdown'); assert session.process.wait(timeout=20) == 0
                finally:
                    session.close()
    log = logpath.read_text()
    assert ': failed' not in log and ' ERR]' not in log and log.count(': passed') == 3
    assert log.count('Shutting down') == log.count('Writing world save snapshot done') == 3
    print('Non-armor equipment defense:3checks/3boots/2actual reloads;648source composition cases per boot,actual Ribcracker equip/repair/removal and original/frozen numeric persistence pass. Full NPC application remains unfinished.')


if __name__ == '__main__':
    check()
