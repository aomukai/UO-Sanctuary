#!/usr/bin/env python3
"""Compare Iron Golem birth against complete source helpers in a disposable server."""
import hashlib
import json
from pathlib import Path
import shutil
import socket
import tempfile
import threading
from check_britain import ROOT, SERVER, Session

STAGE = ROOT / '.local/class-skills/iron-golem-birth'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def check(stage=None, extra_files=None, extra_families=(), extra_inputs=(), extra_checks=(), extra_live_commands=(), extra_wait_results=None):
    stage = stage or STAGE
    with tempfile.TemporaryDirectory(prefix='sanctuary-iron-golem-birth-', dir='/dev/shm') as temp:
        folder = Path(temp)
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
            shutil.copy2(stage / 'native/Distribution' / name, folder / name)
        assemblies = json.loads((SERVER / 'Data/assemblies.json').read_text())
        for name, kind in [('Sanctuary.Content.dll', 'content'), ('Sanctuary.Integration.dll', 'integration')]:
            (folder / 'Assemblies' / name).symlink_to(stage / kind / name)
            if name not in assemblies:
                assemblies.append(name)
        (folder / 'Data/assemblies.json').write_text(json.dumps(assemblies))
        shutil.copytree(ROOT / 'data/Sanctuary', folder / 'Data/Sanctuary')
        probes = {
            'iron-golem-birth-source-cases.json': ROOT / '.local/class-skills/iron-golem-birth-source/cases.json',
            'iron-golem-items-source-probe.json': ROOT / '.local/class-skills/iron-golem-items-source-probe.json',
            'npc-resource-callbacks-source-cases.json': ROOT / '.local/class-skills/npc-resource-callbacks-source/cases.json',
        }
        probes.update(extra_files or {})
        for name, path in probes.items():
            shutil.copy2(path, folder / name)
        sources = [p for directory in ['src/Sanctuary.Content', 'shared', 'tests/Sanctuary.Integration']
                   for p in (ROOT / directory).glob('*.cs')] + [Path(__file__).resolve(), *extra_inputs, *probes.values()]
        inputs = dict(sourceInputs={str(p.relative_to(ROOT)): sha(p) for p in sorted(sources)},
            artifacts={name: sha(folder / name) for name in ['ModernUO.dll', 'Server.dll', 'Assemblies/UOContent.dll', 'Assemblies/Sanctuary.Content.dll', 'Assemblies/Sanctuary.Integration.dll']},
            copiedData={str(p.relative_to(folder)): sha(p) for p in (folder / 'Data/Sanctuary').rglob('*') if p.is_file()})
        (stage / 'birth-inputs.json').write_text(json.dumps(inputs, indent=2) + '\n')
        configpath = folder / 'Configuration/modernuo.json'
        config = json.loads(configpath.read_text())
        with socket.socket() as reservation:
            reservation.bind(('127.0.0.1', 0)); port = reservation.getsockname()[1]
        with socket.socket(type=socket.SOCK_DGRAM) as reservation:
            reservation.bind(('127.0.0.1', 0)); ping = reservation.getsockname()[1]
        config['listeners'] = [f'127.0.0.1:{port}']
        # This native kernel's shutdown requires an initialized PingServer.
        config['settings'].update({'autosave.enabled': 'False', 'pingServer.enabled': 'True', 'pingServer.port': str(ping),
            'sanctuary.graveyard.enabled': 'False', 'world.savePath': 'Saves', 'crashGuard.restartServer': 'False', 'sanctuary.professions.enabled': 'True'})
        configpath.write_text(json.dumps(config))
        waited_results = 0
        with (stage / 'birth-native-checks.log').open('w') as log:
            for boot in range(3):
                session = Session(folder, log)
                try:
                    ready = session.expect(r'create the owner account now\? \(y/n\):|Listening: 127.0.0.1:' + str(port))
                    if ready.group(0).startswith('create'):
                        session.send('n'); session.expect('Listening: 127.0.0.1:' + str(port))
                    commands = ['iron-golem-birth-check', *extra_checks] + [name + ('-check' if boot == 0 else '-reload-check') for name in ['iron-golem-items', 'npc-resource-callbacks', *extra_families]]
                    for command in commands:
                        session.send(command)
                        assert session.expect(command + r': (passed|failed)').group(1) == 'passed'
                        if command in (extra_wait_results or {}):
                            result = extra_wait_results[command]
                            assert session.expect(result + r': (passed|failed)', timeout=30).group(1) == 'passed'
                            waited_results += 1
                    for command in extra_live_commands:
                        with socket.create_connection(('127.0.0.1', port), timeout=5) as client:
                            def drain():
                                try:
                                    while client.recv(65536): pass
                                except OSError: pass
                            threading.Thread(target=drain, daemon=True).start()
                            session.send(command); session.expect(command + ': ready')
                            client.sendall(b'\x73\x00')
                            assert session.expect(command + r'-result: (passed|failed)', timeout=30).group(1) == 'passed'
                    session.send('save'); session.expect('Writing world save snapshot done')
                    for name in ['iron-golem-items-fixtures.json', 'npc-resource-callbacks-fixtures.json', *[family + '-fixtures.json' for family in extra_families]]:
                        shutil.copy2(folder / name, stage / name)
                    session.send('shutdown'); assert session.process.wait(timeout=20) == 0
                finally:
                    session.close()
    text = (stage / 'birth-native-checks.log').read_text()
    assert ': failed' not in text and ' ERR]' not in text and text.count(': passed') == 9 + 3 * (len(extra_families)+len(extra_checks)+len(extra_live_commands)) + waited_results
    assert text.count('Shutting down') == text.count('Writing world save snapshot done') == 3
    print('Iron Golem prepared birth:source/native numeric checks and optional graph scopes pass across3boots/2reloads,with original-item/resource regression. Learned actor remains unfinished.')


if __name__ == '__main__':
    check()
