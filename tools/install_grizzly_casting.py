#!/usr/bin/env python3
"""Install the fully verified learned Grizzly content package, preserving original runtime data."""
import hashlib
import json
from pathlib import Path
import runpy
import shutil
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / '.local/class-skills'
STAGE = BASE / 'grizzly-casting'
SERVER = ROOT / '.local/server'
sha = lambda p: hashlib.sha256(Path(p).read_bytes()).hexdigest()

def hashes(folder):
    return {str(p.relative_to(folder)): sha(p) for p in folder.rglob('*') if p.is_file()}

def generic_outputs():
    return {kind: dict(target=str((BASE / kind).resolve()), symlink=(BASE / kind).is_symlink(), sha256=sha(BASE / kind / ('Sanctuary.' + kind.title() + '.dll'))) for kind in ['content', 'integration']}

def run():
    subprocess.run([sys.executable, 'tools/record_grizzly_casting.py', '--check', '--full-class'], cwd=ROOT, check=True)
    inputs = json.loads((STAGE / 'stats-inputs.json').read_text())
    for name, digest in inputs['sourceInputs'].items():
        assert sha(ROOT / name) == digest, name
    for name, digest in inputs['copiedData'].items():
        assert sha(ROOT / 'data/Sanctuary' / name.removeprefix('Data/Sanctuary/')) == digest, name
    for name in ['ModernUO.dll', 'Server.dll', 'Assemblies/UOContent.dll']:
        assert sha(SERVER / name) == inputs['artifacts'][name], name
    ns = runpy.run_path(str(ROOT / 'dev'))
    ns['require_stopped']('server'); ns['require_stopped']('client')
    backup = BASE / 'pre-grizzly-casting-install'
    assert not backup.exists()
    backup.mkdir()
    restored = ['Assemblies/Sanctuary.Content.dll', 'Data/assemblies.json']
    for name in restored:
        target = backup / 'server' / name
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(SERVER / name, target)
    shutil.copytree(SERVER / 'Data/Sanctuary', backup / 'Sanctuary')
    shutil.copytree(SERVER / 'Saves', backup / 'Saves')
    shutil.copytree(SERVER / 'Configuration', backup / 'Configuration')
    shutil.copy2(ROOT / '.local/client/cuo.dll', backup / 'client-cuo.dll')
    shutil.copy2(ROOT / '.local/content/Sanctuary.Content.dll', backup / 'local-content.dll')
    config = SERVER / 'Configuration/modernuo.json'
    original = config.read_bytes()
    protected = dict(genericOutputs=generic_outputs(), saves=hashes(SERVER / 'Saves'), configuration=hashes(SERVER / 'Configuration'),
        profiles=hashes(ROOT / '.local/client/Profiles'), clientDlls={p.name:sha(p) for p in (ROOT/'.local/client').glob('*.dll')},
        clientSha256=sha(ROOT / '.local/client/cuo.dll'), settingsSha256=sha(ROOT / '.local/client/settings.json'))
    (BASE / 'grizzly-casting-preservation.json').write_text(json.dumps(protected, indent=2) + '\n')
    sys.path.insert(0, str(ROOT / 'tests'))
    from check_britain import Session
    try:
        # Generic class-skills outputs belong to earlier packages.
        # Keep them untouched; use only this immutable dedicated package.
        shutil.copy2(STAGE / 'content/Sanctuary.Content.dll', ROOT / '.local/content/Sanctuary.Content.dll')
        ns['stage_content']()
        settings = json.loads(original)
        settings['settings']['autosave.enabled'] = 'False'
        config.write_text(json.dumps(settings))
        with (BASE / 'grizzly-casting-installed-server.log').open('w') as log:
            session = Session(SERVER, log)
            try:
                session.expect(r'Listening: .*:2593'); session.expect(r'Listening: .*:12000')
                session.send('shutdown'); session.expect('Shutting down')
                assert session.process.wait(timeout=20) == 0
            finally:
                session.close()
        config.write_bytes(original)
        text = (BASE / 'grizzly-casting-installed-server.log').read_text()
        assert ' ERR]' not in text and ': failed' not in text and 'Loading world' in text
        assert hashes(SERVER / 'Saves') == protected['saves'] == hashes(backup / 'Saves')
        assert hashes(SERVER / 'Configuration') == protected['configuration'] == hashes(backup / 'Configuration')
        assert hashes(ROOT / '.local/client/Profiles') == protected['profiles']
        assert {p.name:sha(p) for p in (ROOT/'.local/client').glob('*.dll')} == protected['clientDlls']
        assert sha(ROOT / '.local/client/settings.json') == protected['settingsSha256']
        assert generic_outputs() == protected['genericOutputs']
        assert hashes(SERVER / 'Data/Sanctuary') == {name.removeprefix('Data/Sanctuary/'): digest for name, digest in inputs['copiedData'].items()}
        for name, digest in inputs['copiedData'].items():
            assert sha(SERVER / name) == digest, name
        expected = inputs['artifacts']['Assemblies/Sanctuary.Content.dll']
        assert sha(ROOT / '.local/content/Sanctuary.Content.dll') == sha(SERVER / 'Assemblies/Sanctuary.Content.dll') == expected
        ns['require_stopped']('server'); ns['require_stopped']('client')
    except BaseException:
        ns['require_stopped']('server')
        for name in restored:
            shutil.copy2(backup / 'server' / name, SERVER / name)
        shutil.copy2(backup / 'local-content.dll', ROOT / '.local/content/Sanctuary.Content.dll')
        shutil.rmtree(SERVER / 'Data/Sanctuary'); shutil.copytree(backup / 'Sanctuary', SERVER / 'Data/Sanctuary')
        if hashes(SERVER / 'Configuration') != protected['configuration']:
            shutil.rmtree(SERVER / 'Configuration'); shutil.copytree(backup / 'Configuration', SERVER / 'Configuration')
        if hashes(SERVER / 'Saves') != protected['saves']:
            shutil.rmtree(SERVER / 'Saves'); shutil.copytree(backup / 'Saves', SERVER / 'Saves')
        raise
    finally:
        config.write_bytes(original)
    artifacts = [ROOT / '.local/content/Sanctuary.Content.dll', SERVER / 'Assemblies/Sanctuary.Content.dll',
        BASE / 'grizzly-casting-installed-server.log', BASE / 'grizzly-casting-preservation.json', Path(__file__).resolve(),
        ROOT / 'tools/reference/grizzly-casting-native-evidence.json', ROOT / '.local/client/cuo.dll', backup / 'client-cuo.dll']
    artifacts += [SERVER / name for name in inputs['copiedData']]
    receipt = dict(schemaVersion=1, referenceVersion='3.3.93847', installed=True, handlers=172, druidHandlers=16,
        goalComplete=False, completedClasses=[], originalWorldSmoke=True, smokeReturnCode=0,
        clientUpdated=False, clientReused=True, genericPackageLinksUnchanged=True,
        protected=protected, backup=str(backup.relative_to(ROOT)),
        artifacts={str(p.relative_to(ROOT)): sha(p) for p in artifacts})
    (BASE / 'grizzly-casting-installation.json').write_text(json.dumps(receipt, indent=2) + '\n')
    print('Installed tested172/Druid16 learned Grizzly content package; original-world smoke0, saves/config/client unchanged, generic output links untouched; server stopped.')

if __name__ == '__main__':
    run()
