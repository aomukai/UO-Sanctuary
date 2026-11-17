#!/usr/bin/env python3
"""Check the installed eighth creation choice without touching the installed world."""
import argparse
from contextlib import contextmanager
import hashlib
import json
import os
from pathlib import Path
import shutil
import socket
import subprocess
import tempfile

import check_britain

ROOT = Path(__file__).resolve().parents[1]


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


@contextmanager
def preserved_world(logs):
    with tempfile.TemporaryDirectory(prefix='sanctuary-roster-', dir='/dev/shm') as temp:
        folder = Path(temp)
        try:
            yield folder
        finally:
            for name in ('Configuration', 'Saves', 'Logs'):
                if (folder / name).exists():
                    shutil.copytree(folder / name, logs / name)


def check(runtime, integration, evidence):
    evidence.mkdir()  # Fresh evidence only; retain failures rather than overwrite them.
    originals = {}
    for name in ('.local/server/Saves', '.local/server/Configuration', '.local/client/Profiles',
                 '.local/client/Data'):
        for path in sorted((runtime / name).rglob('*')):
            if path.is_file():
                originals[str(path.relative_to(runtime))] = sha(path)
    for name in ('.local/client/settings.json', '.local/server/Data/assemblies.json'):
        originals[name] = sha(runtime / name)
    (evidence / 'protected-before.json').write_text(json.dumps(originals, indent=2) + '\n')
    server = runtime / '.local/server'
    client = runtime / '.local/client/cuo.dll'
    artifacts = {str(path): sha(path) for path in (
        server / 'ModernUO.dll', server / 'Server.dll', server / 'Assemblies/UOContent.dll',
        runtime / '.local/content/Sanctuary.Content.dll', client, integration)}
    (evidence / 'artifacts.json').write_text(json.dumps(artifacts, indent=2) + '\n')
    check_britain.ROOT = runtime  # Reuse the existing server console/lifecycle helper.
    completed = []
    original_env = os.environ.copy()
    try:
        for female in (False, True):
            logs = evidence / ('female' if female else 'male')
            logs.mkdir()
            with preserved_world(logs) as folder:
                for entry in server.iterdir():
                    if entry.is_file() and entry.suffix in ('.dll', '.json', '.config'):
                        shutil.copy2(entry, folder / entry.name)
                (folder / 'runtimes').symlink_to(server / 'runtimes', target_is_directory=True)
                shutil.copytree(server / 'Configuration', folder / 'Configuration')
                for name in ('Assemblies', 'Data'):
                    (folder / name).mkdir()
                    for entry in (server / name).iterdir():
                        if entry.name not in ('Sanctuary.Content.dll', 'Sanctuary.Integration.dll',
                                              'Sanctuary', 'assemblies.json'):
                            (folder / name / entry.name).symlink_to(entry, target_is_directory=entry.is_dir())
                (folder / 'Assemblies/Sanctuary.Content.dll').symlink_to(runtime / '.local/content/Sanctuary.Content.dll')
                (folder / 'Assemblies/Sanctuary.Integration.dll').symlink_to(integration)
                shutil.copytree(ROOT / 'data/Sanctuary', folder / 'Data/Sanctuary')
                manifest = json.loads((server / 'Data/assemblies.json').read_text())
                assert 'Sanctuary.Integration.dll' not in manifest
                (folder / 'Data/assemblies.json').write_text(json.dumps(manifest + ['Sanctuary.Integration.dll']))
                config_path = folder / 'Configuration/modernuo.json'
                config = json.loads(config_path.read_text())
                with socket.socket() as reservation:
                    reservation.bind(('127.0.0.1', 0))
                    port = reservation.getsockname()[1]
                with socket.socket(type=socket.SOCK_DGRAM) as reservation:
                    reservation.bind(('127.0.0.1', 0))
                    ping_port = reservation.getsockname()[1]
                config['listeners'] = [f'127.0.0.1:{port}']
                config['settings'].update({'autosave.enabled': 'False', 'pingServer.enabled': 'True',
                    'pingServer.port': str(ping_port),
                    'sanctuary.graveyard.enabled': 'False', 'sanctuary.professions.enabled': 'False',
                    'world.savePath': 'Saves', 'crashGuard.restartServer': 'False',
                    'accountHandler.enableAutoAccountCreation': 'True', 'accountHandler.maxAccountsPerIP': '50'})
                config_path.write_text(json.dumps(config))
                env = {**original_env, 'SANCTUARY_TEST_RUNTIME_ROOT': str(runtime),
                    'SANCTUARY_TEST_LOG_DIR': str(logs), 'SANCTUARY_TEST_LOG': str(logs / 'server.log'),
                    'SANCTUARY_TEST_CLIENT_DLL': str(client), 'SANCTUARY_TEST_CLASS_NAMES_UI': '1',
                    'SANCTUARY_TEST_CLASS_NAMES_ID': '108', 'SANCTUARY_TEST_CLASS_NAMES_FEMALE': str(int(female))}
                os.environ.update(env)  # The reused Session starts its server from this environment.
                with (logs / 'server.log').open('w') as log:
                    for boot in range(2):
                        session = check_britain.Session(folder, log)
                        try:
                            if boot == 0:
                                session.expect(r'create the owner account now\? \(y/n\):')
                                session.send('n')
                            session.expect(f'Listening: 127.0.0.1:{port}')
                            if boot == 0:
                                ui_env = {**env, 'SANCTUARY_TEST_CONSOLE_FD': str(session.master)}
                                with (logs / 'ui.log').open('w') as ui_log:
                                    subprocess.run(['xvfb-run', '-a', 'python3', '-u',
                                        str(ROOT / 'tests/check_creation_ui.py'), str(port)],
                                        env=ui_env, pass_fds=[session.master], stdout=ui_log,
                                        stderr=subprocess.STDOUT, check=True, timeout=180)
                            session.send('class-names-ui-check')
                            assert session.expect(r'class-names-ui-check: (passed|failed)').group(1) == 'passed'
                            session.send('save')
                            session.expect('Writing world save snapshot done')
                            session.send('shutdown')
                            assert session.process.wait(timeout=20) == 0
                        finally:
                            session.close()
                completed.append({'wireClass': 108, 'female': female, 'boots': 2, 'passed': True})
                print(f'Creation, matching title/talent heading and saved reload: {"Witch" if female else "Warlock"} passed.', flush=True)
    finally:
        os.environ.clear()
        os.environ.update(original_env)
        changed = [name for name, digest in originals.items() if sha(runtime / name) != digest]
        (evidence / 'protection-result.json').write_text(json.dumps({'changed': changed}, indent=2) + '\n')
        assert not changed, changed
    (evidence / 'result.json').write_text(json.dumps({'variants': completed,
        'allEightChoicesClicked': True, 'bothSexControlsClicked': True,
        'originalFilesUnchanged': len(originals), 'combatVerified': False}, indent=2) + '\n')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--runtime', required=True, type=Path)
    parser.add_argument('--integration', required=True, type=Path)
    parser.add_argument('--evidence', required=True, type=Path)
    args = parser.parse_args()
    check(args.runtime.resolve(), args.integration.resolve(), args.evidence.resolve())
