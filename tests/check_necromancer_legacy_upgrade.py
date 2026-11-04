#!/usr/bin/env python3
"""Actual installed-writer -> current-reader Necromancer save migration.

All boots use a fresh disposable world. Installed saves/configuration and
assemblies are only hashed/read, never opened by a server or overwritten.
"""
import hashlib
import json
import shutil
import socket
import sys
from pathlib import Path

from check_britain import ROOT, Session
sys.path.insert(0, str(ROOT / 'tools'))
from snapshot_skill_build import check as snapshot

STAGE = ROOT / '.local/class-skills/necro-legacy-upgrade'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def hashes(folder):
    return {str(p.relative_to(folder)):sha(p) for p in sorted(folder.rglob('*')) if p.is_file()}


def copy(source, target):
    if target.is_symlink():
        target.unlink()
    shutil.copy2(source, target)


def check():
    snapshot(STAGE / 'pre-build-2.json')
    installation = json.loads((ROOT / 'tools/reference/creeper-frame-installation-evidence.json').read_text())
    source_root = Path(installation['runtimeRoot'])
    installed = source_root / '.local/server'
    before = {p:sha(source_root / p) for p in installation['installedArtifacts']}
    assert before == installation['installedArtifacts']
    protection = json.loads((ROOT / '.local/class-skills/creeper-frame-installation.json').read_text())['protected']
    for kind, relative in (('saves', 'Saves'), ('configuration', 'Configuration')):
        assert hashes(installed / relative) == protection[kind]
    old_receipt = json.loads((ROOT / 'tools/reference/creeper-frame-native-evidence.json').read_text())
    old_artifacts = old_receipt['inputInventory']['artifacts']
    old_integration = source_root / '.local/class-skills/creeper-frame-correction/integration/Sanctuary.Integration.dll'
    assert sha(old_integration) == old_artifacts['Assemblies/Sanctuary.Integration.dll']
    for name in ('ModernUO.dll', 'Server.dll', 'Assemblies/UOContent.dll', 'Assemblies/Sanctuary.Content.dll'):
        assert sha(installed / name) == old_artifacts[name], name
    runtime = STAGE / 'runtime-1'
    runtime.mkdir()  # Never reuse a partially written or failed world.
    for entry in installed.iterdir():
        if entry.is_file() and entry.suffix in ('.dll', '.json', '.config'):
            copy(entry, runtime / entry.name)
    (runtime / 'runtimes').symlink_to(installed / 'runtimes', target_is_directory=True)
    shutil.copytree(installed / 'Configuration', runtime / 'Configuration')
    for name in ('Assemblies', 'Data'):
        (runtime / name).mkdir()
        for entry in (installed / name).iterdir():
            if entry.name not in ('Sanctuary', 'assemblies.json', 'Sanctuary.Integration.dll'):
                if entry.is_dir():
                    (runtime / name / entry.name).symlink_to(entry, target_is_directory=True)
                else:
                    copy(entry, runtime / name / entry.name)
    shutil.copytree(installed / 'Data/Sanctuary', runtime / 'Data/Sanctuary')
    assemblies = json.loads((installed / 'Data/assemblies.json').read_text())
    for name, path in (('Sanctuary.Integration.dll', old_integration),
                       ('Sanctuary.LegacySeed.dll', STAGE / 'legacy-seed/Sanctuary.LegacySeed.dll')):
        copy(path, runtime / 'Assemblies' / name)
        if name not in assemblies:
            assemblies.append(name)
    (runtime / 'Data/assemblies.json').write_text(json.dumps(assemblies))
    config_path = runtime / 'Configuration/modernuo.json'
    config = json.loads(config_path.read_text())
    with socket.socket() as reserved:
        reserved.bind(('127.0.0.1', 0)); port = reserved.getsockname()[1]
    with socket.socket(type=socket.SOCK_DGRAM) as reserved:
        reserved.bind(('127.0.0.1', 0)); ping = reserved.getsockname()[1]
    config['listeners'] = [f'127.0.0.1:{port}']
    config['settings'].update({'autosave.enabled':'False', 'pingServer.enabled':'True', 'pingServer.port':str(ping),
        'sanctuary.graveyard.enabled':'False', 'world.savePath':'Saves', 'crashGuard.restartServer':'False',
        'sanctuary.professions.enabled':'True'})
    config_path.write_text(json.dumps(config))
    evidence = dict(oldArtifacts=old_artifacts, actualOldArtifacts={name:sha(runtime / name) for name in old_artifacts},
        oldCopiedData=hashes(runtime / 'Data/Sanctuary'), seedAssemblySha256=sha(runtime / 'Assemblies/Sanctuary.LegacySeed.dll'),
        sourceInputs={str(p.relative_to(ROOT)):sha(p) for p in [Path(__file__).resolve(),
            ROOT / 'tests/Sanctuary.LegacySeed/LegacySeed.cs', ROOT / 'tests/Sanctuary.LegacySeed/Sanctuary.LegacySeed.csproj',
            ROOT / 'tests/Sanctuary.Integration/NecromancerLegacyUpgradeChecks.cs']}, boots=[])
    assert evidence['actualOldArtifacts'] == old_artifacts
    for boot in range(3):
        if boot == 1:
            for name in ('ModernUO.dll', 'Server.dll', 'Logger.dll', 'Assemblies/UOContent.dll'):
                copy(STAGE / 'native/Distribution' / name, runtime / name)
            for kind in ('content', 'integration'):
                name = 'Sanctuary.' + kind.title() + '.dll'
                copy(STAGE / kind / name, runtime / 'Assemblies' / name)
            (runtime / 'Assemblies/Sanctuary.LegacySeed.dll').unlink()
            assemblies.remove('Sanctuary.LegacySeed.dll')
            (runtime / 'Data/assemblies.json').write_text(json.dumps(assemblies))
            shutil.rmtree(runtime / 'Data/Sanctuary')
            shutil.copytree(ROOT / 'data/Sanctuary', runtime / 'Data/Sanctuary')
            evidence['currentArtifacts'] = {name:sha(runtime / name) for name in old_artifacts}
            evidence['currentCopiedData'] = hashes(runtime / 'Data/Sanctuary')
        with (STAGE / f'boot-{boot}.log').open('w') as log:
            session = Session(runtime, log)
            try:
                ready = session.expect(r'create the owner account now\? \(y/n\):|Listening: 127.0.0.1:' + str(port))
                if ready.group(0).startswith('create'):
                    session.send('n'); session.expect('Listening: 127.0.0.1:' + str(port))
                commands = ('class-skills-check', 'necro-legacy-seed') if boot == 0 else ('necro-legacy-upgrade-check',)
                for command in commands:
                    session.send(command)
                    assert session.expect(command + r': (passed|failed)').group(1) == 'passed'
                session.send('save'); session.expect('Writing world save snapshot done')
                session.send('shutdown'); assert session.process.wait(timeout=20) == 0
            finally:
                session.close()
        text = (STAGE / f'boot-{boot}.log').read_text()
        assert ': failed' not in text and ' ERR]' not in text
        saved = STAGE / f'world-{boot}'
        shutil.copytree(runtime / 'Saves', saved)
        for name in ('class-skills-checks.json', 'necro-legacy-seed.json', 'necro-legacy-progress.json'):
            if (runtime / name).exists():
                copy(runtime / name, STAGE / f'boot-{boot}-{name}')
        evidence['boots'].append(dict(writer='installed' if boot == 0 else 'current', saves=hashes(saved)))
        (STAGE / 'migration-inputs.json').write_text(json.dumps(evidence, indent=2) + '\n')
    assert {p:sha(source_root / p) for p in before} == before
    for kind, relative in (('saves', 'Saves'), ('configuration', 'Configuration')):
        assert hashes(installed / relative) == protection[kind]
    progress = json.loads((runtime / 'necro-legacy-progress.json').read_text())
    seed = json.loads((runtime / 'necro-legacy-seed.json').read_text())
    assert progress == dict(Generation=2, Remaining256=seed['Armor'] * 256 - 128)
    print('Actual earlier-build Necromancer writer -> current reader: three boots/two reloads/three saves, original wounded actors, fractional mana and unknown-provenance armor retained; installed world untouched.')


if __name__ == '__main__':
    check()
