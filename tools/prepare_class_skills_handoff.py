#!/usr/bin/env python3
"""Prepare the verified class package and load a copy of the original world."""
import argparse
import hashlib
import io
import json
import os
from pathlib import Path
import shutil
import socket
import struct
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]
CLASS = ROOT / '.local/class-skills/warlock-lifecycle-acceptance'
CLIENT = ROOT / '.local/class-skills/consume-learned/client'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def hashes(folder):
    return {str(p.relative_to(folder)): sha(p) for p in sorted(folder.rglob('*')) if p.is_file()}


def write(path, value):
    path.write_text(json.dumps(value, indent=2) + '\n')
    with path.open('rb') as stream:
        os.fsync(stream.fileno())


def identities(path):
    """Read ModernUO v4/v5 type-table indexes without reading account payloads."""
    data = path.read_bytes()
    stream = io.BytesIO(data)

    def read(fmt):
        return struct.unpack('<' + fmt, stream.read(struct.calcsize('<' + fmt)))[0]

    version = read('i')
    assert version in (4, 5), ('Unsupported index format', path, version)
    if version == 5:
        read('q')  # Save anchor; entity creation ticks are stored separately.
    types = []
    for _ in range(read('i')):
        length = shift = 0
        while True:
            byte = read('B')
            length |= (byte & 127) << shift
            shift += 7
            assert shift <= 35
            if byte < 128:
                break
        types.append(stream.read(length).decode('utf-8'))
    result = {}
    for _ in range(read('i')):
        kind, serial, created, offset, length = struct.unpack('<HIqqi', stream.read(26))
        assert serial not in result and offset >= 0 and length >= 0
        result[serial] = dict(type=types[kind], created=created)
    assert stream.tell() == len(data)
    return result


def protected(runtime):
    return {name: hashes(runtime / name) for name in (
        '.local/server/Saves', '.local/server/Configuration', '.local/client/Profiles',
        '.local/server/Data/Sanctuary', '.local/content', '.local/client/Data')}


def selected_files(runtime):
    paths = []
    for name in ('.local/server', '.local/server/Assemblies', '.local/client'):
        paths.extend(p for p in (runtime / name).iterdir()
                     if p.is_file() and p.suffix in ('.dll', '.json', '.config', '.so', '.0'))
    paths.append(runtime / '.local/server/Data/assemblies.json')
    return {str(p.relative_to(runtime)): sha(p) for p in sorted(set(paths))}


def prepare(runtime, stage):
    assert runtime != ROOT and not stage.exists(), 'Use a fresh external stage'
    assert not str(stage).startswith(str(ROOT)) and '.codex' not in stage.parts
    sys.path.insert(0, str(ROOT / 'tools'))
    import skill_checkpoint_evidence as evidence
    evidence.protection('tools/reference/consume-learned-client-evidence.json', 44)
    import runpy
    dev = runpy.run_path(str(runtime / 'dev'))
    dev['require_stopped']('server')
    dev['require_stopped']('client')
    born = json.loads((CLASS / 'birth-inputs.json').read_text())
    receipt = json.loads((ROOT / 'tools/reference/warlock-lifecycle-acceptance-native-evidence.json').read_text())
    assert receipt['nativeMarkers'] == 906 and receipt['artifacts'] == born['artifacts']
    for path, digest in born['sourceInputs'].items():
        assert sha(ROOT / path) == digest, path
    for path, digest in born['copiedData'].items():
        assert sha(ROOT / 'data' / path.removeprefix('Data/')) == digest, path
    native = (CLASS / 'native/Distribution').resolve()
    for name in ('ModernUO.dll', 'Server.dll', 'Assemblies/UOContent.dll'):
        assert sha(native / name) == born['artifacts'][name], name
    content = CLASS / 'content/Sanctuary.Content.dll'
    assert sha(content) == born['artifacts']['Assemblies/Sanctuary.Content.dll']
    client_receipt = json.loads((ROOT / 'tools/reference/consume-learned-client-evidence.json').read_text())
    for path, digest in client_receipt['files'].items():
        assert sha(ROOT / path) == digest, path
    stage.mkdir(parents=True)
    original = protected(runtime)
    original_files = selected_files(runtime)
    backup = stage / 'original'
    for name in original:
        if (runtime / name).is_dir():
            shutil.copytree(runtime / name, backup / name)
        else:
            assert not (runtime / name).exists() and original[name] == {}
    for path in original_files:
        destination = backup / path
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(runtime / path, destination)
    assert protected(backup) == original
    assert all(sha(backup / path) == digest for path, digest in original_files.items())
    server = stage / 'server'
    server.mkdir()
    for source in (runtime / '.local/server', native):
        for path in source.iterdir():
            if path.is_file() and path.suffix in ('.dll', '.json', '.config'):
                shutil.copy2(path, server / path.name)
    (server / 'runtimes').symlink_to(runtime / '.local/server/runtimes', target_is_directory=True)
    for name in ('Assemblies', 'Data'):
        shutil.copytree(runtime / '.local/server' / name, server / name)
    for path in (native / 'Assemblies').iterdir():
        if path.is_file() and path.suffix == '.dll':
            shutil.copy2(path, server / 'Assemblies' / path.name)
    shutil.copy2(content, server / 'Assemblies/Sanctuary.Content.dll')
    shutil.rmtree(server / 'Data/Sanctuary')
    shutil.copytree(ROOT / 'data/Sanctuary', server / 'Data/Sanctuary')
    assert hashes(server / 'Data/Sanctuary') == {
        path.removeprefix('Data/Sanctuary/'): digest for path, digest in born['copiedData'].items()}
    assert json.loads((server / 'Data/assemblies.json').read_text()) == ['UOContent.dll', 'Sanctuary.Content.dll']
    shutil.copy2(content, server / 'Assemblies/Sanctuary.Content.dll')
    shutil.rmtree(server / 'Data/Sanctuary')
    shutil.copytree(ROOT / 'data/Sanctuary', server / 'Data/Sanctuary')
    assert json.loads((server / 'Data/assemblies.json').read_text()) == ['UOContent.dll', 'Sanctuary.Content.dll']
    assert not (server / 'Assemblies/Sanctuary.Integration.dll').exists()
    for name in ('Saves', 'Configuration'):
        shutil.copytree(runtime / '.local/server' / name, server / name)
    client = stage / 'client'
    client.mkdir()
    for path in (runtime / '.local/client').iterdir():
        if path.is_dir() and path.name not in ('Profiles', 'Logs', 'Screenshots'):
            shutil.copytree(path, client / path.name, symlinks=True)
    for path in CLIENT.iterdir():
        if path.is_file() and path.suffix not in ('.pdb', '.nupkg'):
            shutil.copy2(path, client / path.name)
    shutil.copy2(runtime / '.local/client/settings.json', client / 'settings.json')
    if (runtime / '.local/client/Profiles').is_dir():
        shutil.copytree(runtime / '.local/client/Profiles', client / 'Profiles')
    assert hashes(server / 'Saves') == original['.local/server/Saves']
    assert hashes(server / 'Data/Sanctuary') == {p.removeprefix('Data/Sanctuary/'): d for p, d in born['copiedData'].items()}
    mobiles = identities(server / 'Saves/Mobiles/Mobiles.idx')
    players_pets = {serial: row for serial, row in mobiles.items()
                   if row['type'] in ('Server.Mobiles.PlayerMobile', 'Sanctuary.SkeletonGuardian')}
    assert sum(r['type'] == 'Server.Mobiles.PlayerMobile' for r in players_pets.values()) == 2
    assert sum(r['type'] == 'Sanctuary.SkeletonGuardian' for r in players_pets.values()) == 2
    manifest = dict(referenceVersion='3.3.93847', sourceCommit=subprocess.check_output(
        ['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip(), runtimeRoot=str(runtime),
        classReceiptSha256=sha(ROOT / 'tools/reference/warlock-lifecycle-acceptance-native-evidence.json'),
        artifacts=born['artifacts'], original=original, originalFiles=original_files,
        originalPlayerPetIdentities=players_pets, candidateClient=hashes(client),
        handlers=240, clientPatches=44, scriptSha256=sha(Path(__file__).resolve()),
        clientReceiptSha256=sha(ROOT / 'tools/reference/consume-learned-client-evidence.json'),
        candidateServer={p: d for p, d in hashes(server).items()
                         if not p.startswith(('runtimes/', 'Saves/', 'Configuration/'))},
        installed=False, graphicalGameplayVerified=False)
    write(stage / 'prepared-inputs.json', manifest)
    assert protected(runtime) == original and selected_files(runtime) == original_files
    print('Prepared complete package; original 38 saves, configuration, client and build backed up; two players/two Skeleton Guardians identified.')


def smoke(stage):
    manifest = json.loads((stage / 'prepared-inputs.json').read_text())
    assert not (stage / 'world-smoke.json').exists(), 'Preserve previous results'
    server = stage / 'server'
    config = server / 'Configuration/modernuo.json'
    settings = json.loads(config.read_text())
    with socket.socket() as sock:
        sock.bind(('127.0.0.1', 0)); port = sock.getsockname()[1]
    with socket.socket(type=socket.SOCK_DGRAM) as sock:
        sock.bind(('127.0.0.1', 0)); ping = sock.getsockname()[1]
    settings['listeners'] = [f'127.0.0.1:{port}']
    settings['settings'].update({'autosave.enabled': 'False', 'crashGuard.restartServer': 'False',
        'world.savePath': 'Saves', 'pingServer.enabled': 'True', 'pingServer.port': str(ping)})
    write(config, settings)
    sys.path.insert(0, str(ROOT / 'tests'))
    from check_britain import Session
    saved = []
    with (stage / 'world-smoke.log').open('w') as log:
        for boot in range(3):
            session = Session(server, log)
            try:
                session.expect(r'Listening: 127.0.0.1:' + str(port))
                session.expect(r'Listening: .*:' + str(ping))
                session.send('save')
                session.expect('Writing world save snapshot done')
                session.send('shutdown')
                session.expect('Shutting down')
                assert session.process.wait(timeout=20) == 0
            finally:
                session.close()
            counts = {}
            for kind in ('Mobiles', 'Items'):
                path = Path('.local/server/Saves') / kind / (kind + '.idx')
                original = identities(stage / 'original' / path)
                current = identities(server / 'Saves' / kind / (kind + '.idx'))
                assert current == original, f'Original {kind} identity set changed'
                counts[kind] = len(current)
            mobiles = identities(server / 'Saves/Mobiles/Mobiles.idx')
            assert all(mobiles.get(int(serial)) == row for serial, row in manifest['originalPlayerPetIdentities'].items()), 'Original player/pet serial, type or creation identity changed'
            saved.append(dict(boot=boot, saves=hashes(server / 'Saves'), mobileCount=len(mobiles),
                originalMobileIdentities=counts['Mobiles'], originalItemIdentities=counts['Items']))
    text = (stage / 'world-smoke.log').read_text()
    assert ' ERR]' not in text and ': failed' not in text and 'create the owner account now?' not in text
    assert text.count('Shutting down') == text.count('Writing world save snapshot done') == 3
    runtime = Path(manifest['runtimeRoot'])
    assert protected(runtime) == manifest['original']
    assert selected_files(runtime) == manifest['originalFiles']
    write(stage / 'world-smoke.json', dict(boots=3, saves=3, reloads=2, originalPlayers=2,
        originalSkeletonGuardians=2, identitiesPreserved=True, snapshots=saved,
        originalRuntimeUnchanged=True, installed=False, graphicalGameplayVerified=False))
    print('Original-world copy: three saves/two reloads pass; both players and both original Skeleton Guardians preserved; installed world unchanged.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('action', choices=('prepare', 'smoke'))
    parser.add_argument('--runtime-root', type=Path, default=Path('/media/aomukai/FILES/Sanctuary'))
    parser.add_argument('--stage-root', type=Path, required=True)
    args = parser.parse_args()
    if args.action == 'prepare':
        prepare(args.runtime_root.resolve(), args.stage_root.resolve())
    else:
        smoke(args.stage_root.resolve())
