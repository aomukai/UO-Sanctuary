#!/usr/bin/env python3
"""Verify source regeneration, actual timers and learned Revive persistence."""
import hashlib
import json
import os
from pathlib import Path
import shutil
import socket
import tempfile
import threading
from check_britain import ROOT, SERVER, Session
from check_class_skills import drain_fixture_client

STAGE = ROOT / '.local/class-skills/revive-regeneration-candidate'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def check():
    native = Path(os.environ.get('SANCTUARY_NATIVE_ROOT', str(ROOT))) / 'upstream/ModernUO/Distribution'
    with tempfile.TemporaryDirectory(prefix='sanctuary-revive-regeneration-', dir='/dev/shm') as temporary:
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
        (STAGE / 'recovery-inputs.json').write_text(json.dumps(inputs, indent=2) + '\n')
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
        logpath = STAGE / 'recovery-native-checks.log'
        with logpath.open('w') as log:
            for boot in range(3):
                session = Session(folder, log)
                try:
                    ready = session.expect(r'create the owner account now\? \(y/n\):|Listening: 127.0.0.1:' + str(port))
                    if ready.group(0).startswith('create'):
                        session.send('n'); session.expect('Listening: 127.0.0.1:' + str(port))
                    for family in ['revive-cast','revive-regeneration']:
                        command = family + ('-check' if boot == 0 else '-reload-check')
                        session.send(command)
                        assert session.expect(command + r': (passed|failed)').group(1) == 'passed', logpath.read_text()[-5000:]
                    with socket.create_connection(('127.0.0.1', port), timeout=5) as client:
                        threading.Thread(target=drain_fixture_client, args=(client,), daemon=True).start()
                        session.send('revive-regeneration-live-check')
                        assert session.expect(r'revive-regeneration-live-check: (passed|failed)').group(1) == 'passed'
                        ready = session.expect(r'revive-regeneration-live: ready|revive-regeneration-live-result: failed')
                        assert ready.group(0).endswith('ready'),logpath.read_text()[-5000:]
                        client.sendall(b'\x73\x00')
                        assert session.expect(r'revive-regeneration-live-result: (passed|failed)',timeout=35).group(1) == 'passed',logpath.read_text()[-5000:]
                    session.send('save'); session.expect('Writing world save snapshot done')
                    for fixture in ['revive-regeneration-fixtures.json','revive-cast-fixtures.json']:
                        shutil.copy2(folder / fixture, STAGE / ('isolated-' + fixture))
                    session.send('shutdown'); assert session.process.wait(timeout=20) == 0
                finally:
                    session.close()
    log = logpath.read_text()
    assert ': failed' not in log and ' ERR]' not in log and log.count(': passed') == 12
    assert log.count('Shutting down') == log.count('Writing world save snapshot done') == 3
    print('Revive regeneration:12checks/3boots/2actual reloads;fractional frames,blocks,inactive/full resets,native/Graveyard exclusions,live timer control and optional-rate persistence pass.')


if __name__ == '__main__':
    check()
