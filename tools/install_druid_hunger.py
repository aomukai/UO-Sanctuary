#!/usr/bin/env python3
"""Install the verified candidate into a separate stopped runtime, preserving its world/client."""
import argparse
import hashlib
import json
from pathlib import Path
import runpy
import shutil
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / '.local/class-skills'
STAGE = BASE / 'druid-hunger'


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def hashes(folder):
    return {str(path.relative_to(folder)):sha(path) for path in folder.rglob('*') if path.is_file()}


def generic_outputs(runtime):
    base = runtime / '.local/class-skills'
    return {kind:dict(target=str((base / kind).resolve()), symlink=(base / kind).is_symlink(),
        sha256=sha(base / kind / ('Sanctuary.' + kind.title() + '.dll'))) for kind in ['content', 'integration']}


def run(runtime):
    runtime = runtime.resolve()
    server = runtime / '.local/server'
    client = runtime / '.local/client'
    subprocess.run([sys.executable, str(ROOT / 'tools/record_druid_hunger.py'), '--check', '--full-class'], cwd=ROOT, check=True)
    inputs = json.loads((STAGE / 'class-inputs.json').read_text())
    for name, digest in inputs['sourceInputs'].items():
        assert sha(ROOT / name) == digest, name
    for name, digest in inputs['copiedData'].items():
        assert sha(ROOT / 'data/Sanctuary' / name.removeprefix('Data/Sanctuary/')) == digest, name
    for name in ['ModernUO.dll', 'Server.dll', 'Assemblies/UOContent.dll']:
        assert sha(server / name) == inputs['artifacts'][name], name
    manifest = json.loads((server / 'Data/assemblies.json').read_text())
    assert 'Sanctuary.Content.dll' in manifest and 'Sanctuary.Integration.dll' not in manifest
    ns = runpy.run_path(str(runtime / 'dev'))
    ns['require_stopped']('server')
    ns['require_stopped']('client')
    installed = [runtime / '.local/content/Sanctuary.Content.dll', server / 'Assemblies/Sanctuary.Content.dll']
    assert all(path.is_file() and not path.is_symlink() for path in installed)
    backup = runtime / '.local/class-skills/pre-druid-hunger-install'
    assert not backup.exists()
    backup.mkdir()
    for source, relative in [(installed[0], 'local-content.dll'), (installed[1], 'server-content.dll'),
        (server / 'Data/assemblies.json', 'assemblies.json'), (client / 'cuo.dll', 'client-cuo.dll')]:
        shutil.copy2(source, backup / relative)
    for name, source in [('Sanctuary', server / 'Data/Sanctuary'), ('Saves', server / 'Saves'), ('Configuration', server / 'Configuration')]:
        shutil.copytree(source, backup / name)
    protected = dict(genericOutputs=generic_outputs(runtime), saves=hashes(server / 'Saves'), configuration=hashes(server / 'Configuration'),
        profiles=hashes(client / 'Profiles'), clientDlls={path.name:sha(path) for path in client.glob('*.dll')},
        clientSha256=sha(client / 'cuo.dll'), settingsSha256=sha(client / 'settings.json'), assembliesSha256=sha(server / 'Data/assemblies.json'))
    preservation = BASE / 'druid-hunger-preservation.json'
    preservation.write_text(json.dumps(protected, indent=2) + '\n')
    configuration = server / 'Configuration/modernuo.json'
    original = configuration.read_bytes()
    logpath = BASE / 'druid-hunger-installed-server.log'
    sys.path.insert(0, str(ROOT / 'tests'))
    from check_britain import Session
    try:
        for target in installed:
            pending = target.with_suffix('.dll.hunger-pending')
            assert not pending.exists()
            shutil.copy2(STAGE / 'content/Sanctuary.Content.dll', pending)
            pending.replace(target)
        # The main checkout can stay on its original source until smoke passes.
        # Use only the already-verified candidate data,never a mixed main tree.
        shutil.copytree(ROOT / 'data/Sanctuary', server / 'Data/Sanctuary', dirs_exist_ok=True)
        assert hashes(server / 'Data/Sanctuary') == {name.removeprefix('Data/Sanctuary/'):digest for name,digest in inputs['copiedData'].items()}
        settings = json.loads(original)
        settings['settings']['autosave.enabled'] = 'False'
        settings['settings']['crashGuard.restartServer'] = 'False'
        configuration.write_text(json.dumps(settings))
        with logpath.open('w') as log:
            session = Session(server, log)
            try:
                session.expect(r'Listening: .*:2593')
                session.expect(r'Listening: .*:12000')
                session.send('shutdown')
                session.expect('Shutting down')
                assert session.process.wait(timeout=20) == 0
            finally:
                session.close()
        configuration.write_bytes(original)
        text = logpath.read_text()
        assert ' ERR]' not in text and ': failed' not in text and 'Loading world' in text
        assert 'create the owner account now?' not in text
        assert hashes(server / 'Saves') == protected['saves'] == hashes(backup / 'Saves')
        assert hashes(server / 'Configuration') == protected['configuration'] == hashes(backup / 'Configuration')
        assert hashes(client / 'Profiles') == protected['profiles']
        assert {path.name:sha(path) for path in client.glob('*.dll')} == protected['clientDlls']
        assert sha(client / 'settings.json') == protected['settingsSha256']
        assert sha(server / 'Data/assemblies.json') == protected['assembliesSha256']
        assert generic_outputs(runtime) == protected['genericOutputs']
        for path in installed:
            assert sha(path) == inputs['artifacts']['Assemblies/Sanctuary.Content.dll']
        ns['require_stopped']('server')
        ns['require_stopped']('client')
    except BaseException:
        ns['require_stopped']('server')
        shutil.copy2(backup / 'local-content.dll', installed[0])
        shutil.copy2(backup / 'server-content.dll', installed[1])
        shutil.copy2(backup / 'assemblies.json', server / 'Data/assemblies.json')
        shutil.rmtree(server / 'Data/Sanctuary')
        shutil.copytree(backup / 'Sanctuary', server / 'Data/Sanctuary')
        for name in ['Configuration', 'Saves']:
            if hashes(server / name) != hashes(backup / name):
                shutil.rmtree(server / name)
                shutil.copytree(backup / name, server / name)
        raise
    finally:
        configuration.write_bytes(original)
    receipt = dict(referenceVersion='3.3.93847', installed=True, handlers=179, druidHandlers=23, completedClasses=[], goalComplete=False,
        runtimeRoot=str(runtime), originalWorldSmoke=True, smokeReturnCode=0, clientUpdated=False, genericPackageLinksUnchanged=True,
        sourceCommitAtInstall=subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip(),
        runtimeSourceBefore=subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=runtime, text=True).strip(),
        protected=protected, backup=str(backup.relative_to(runtime)),
        sourceArtifacts={str(path.relative_to(ROOT)):sha(path) for path in [Path(__file__).resolve(), ROOT / 'tools/reference/druid-hunger-native-evidence.json', logpath, preservation]},
        installedArtifacts={str(path.relative_to(runtime)):sha(path) for path in installed + [server / name for name in inputs['copiedData']]})
    (BASE / 'druid-hunger-installation.json').write_text(json.dumps(receipt, indent=2) + '\n')
    print('Hunger179/Druid23installed:original-world smoke0,saves/config/client/generic outputs unchanged; runtime source synchronization follows separately.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--runtime-root', type=Path, required=True)
    run(parser.parse_args().runtime_root)
