#!/usr/bin/env python3
"""Promote qualified Bash/Find Item changes while preserving the original world."""
import json
from pathlib import Path
import runpy
import shutil
import sys

from prepare_class_skills_handoff import hashes, sha, write

ROOT = Path(__file__).resolve().parents[1]
RUNTIME = Path('/media/aomukai/FILES/Sanctuary')
QUALIFIED = ROOT / '.local/class-skills/find-item-adoption'
CLIENT = ROOT / '.local/class-skills/find-item-client'
STAGE = ROOT / '.local/class-skills/class-closing-installation'


def preserved():
    original = json.loads((ROOT / '.local/class-skills/class-skills-handoff/prepared-inputs.json').read_text())
    for name in ('.local/server/Saves', '.local/server/Configuration', '.local/client/Data', '.local/client/Profiles'):
        assert hashes(RUNTIME / name) == original['original'][name], name
    assert sha(RUNTIME / '.local/client/settings.json') == original['originalFiles']['.local/client/settings.json']


def install():
    assert not STAGE.exists(), 'Retain installation attempts and rollback independently'
    destination = Path('/home/aomukai/Sanctuary-checkpoint-archives/revive-respec/class-closing-installation-1')
    assert not destination.exists()
    sys.path.insert(0, str(ROOT / 'tools'))
    from record_find_item_adoption import record
    record(check=True)
    dev = runpy.run_path(str(RUNTIME / 'dev'))
    dev['require_stopped']('server'); dev['require_stopped']('client')
    preserved()
    previous = json.loads((ROOT / '.local/class-skills/main-consolidation/installation-1/installed.json').read_text())
    for name, digest in previous['installedFiles'].items():
        assert sha(RUNTIME / name) == digest, name
    born = json.loads((QUALIFIED / 'birth-inputs.json').read_text())
    assert sha(QUALIFIED / 'content/Sanctuary.Content.dll') == born['artifacts']['Assemblies/Sanctuary.Content.dll']
    for name in ('ModernUO.dll', 'Server.dll', 'Assemblies/UOContent.dll'):
        assert sha(RUNTIME / '.local/server' / name) == born['artifacts'][name]
    sources = {}
    for target in ('.local/content/Sanctuary.Content.dll', '.local/server/Assemblies/Sanctuary.Content.dll'):
        sources[target] = QUALIFIED / 'content/Sanctuary.Content.dll'
    sources['.local/integration/Sanctuary.Integration.dll'] = QUALIFIED / 'integration-3/Sanctuary.Integration.dll'
    for source in (CLIENT / 'client').iterdir():
        if source.is_file() and source.suffix not in ('.pdb', '.nupkg') and source.name != 'settings.json':
            for folder in ('.local/client', 'upstream/ClassicUO/bin/Debug/net10.0'):
                sources[folder + '/' + source.name] = source
    name = 'src/ClassicUO.Client/Game/ClassCombatClient.cs'
    client_inputs = json.loads((CLIENT / 'source-inputs-1.json').read_text())
    target = 'upstream/ClassicUO/' + name
    assert sha(RUNTIME / target) == client_inputs['originalSha256']
    sources[target] = CLIENT / 'source' / target
    sources = {name: source for name, source in sources.items()
               if not (RUNTIME / name).exists() or sha(RUNTIME / name) != sha(source)}
    destination.mkdir(); STAGE.symlink_to(destination, target_is_directory=True)
    before = {}
    for name, source in sources.items():
        target = RUNTIME / name
        assert not target.is_symlink(), name
        before[name] = sha(target) if target.exists() else None
        if target.exists():
            backup = STAGE / 'rollback' / name
            backup.parent.mkdir(parents=True, exist_ok=True); shutil.copy2(target, backup)
            assert sha(backup) == before[name]
    planned = {name: dict(source=str(source), sha256=sha(source)) for name, source in sources.items()}
    write(STAGE / 'plan.json', dict(previousFiles=before, files=planned, originalSavesUnchanged=True))
    configpath = RUNTIME / '.local/server/Configuration/modernuo.json'
    original_config = configpath.read_bytes()
    try:
        for name, source in sources.items():
            target = RUNTIME / name; target.parent.mkdir(parents=True, exist_ok=True)
            pending = target.with_name(target.name + '.class-closing-pending')
            assert not pending.exists()
            shutil.copy2(source, pending); assert sha(pending) == planned[name]['sha256']
            pending.replace(target)
        assert json.loads((RUNTIME / '.local/server/Data/assemblies.json').read_text()) == ['UOContent.dll', 'Sanctuary.Content.dll']
        assert not (RUNTIME / '.local/server/Assemblies/Sanctuary.Integration.dll').exists()
        config = json.loads(original_config)
        config['settings'].update({'autosave.enabled': 'False', 'crashGuard.restartServer': 'False'})
        configpath.write_text(json.dumps(config))
        sys.path.insert(0, str(ROOT / 'tests'))
        from check_britain import Session
        with (STAGE / 'installed-world-smoke.log').open('w') as log:
            session = Session(RUNTIME / '.local/server', log)
            try:
                session.expect('Listening: 127.0.0.1:2593')
                session.send('shutdown'); session.expect('Shutting down')
                assert session.process.wait(timeout=20) == 0
            finally:
                session.close()
        text = (STAGE / 'installed-world-smoke.log').read_text()
        assert 'Loading world done (4698 items, 572 mobiles)' in text
        assert ' ERR]' not in text and 'Saving world' not in text
        configpath.write_bytes(original_config)
        preserved()
        dev['stage_content']()
        for name, row in planned.items():
            assert sha(RUNTIME / name) == row['sha256'], name
        assert hashes(RUNTIME / '.local/server/Data/Sanctuary') == hashes(ROOT / 'data/Sanctuary')
        dev['require_stopped']('server'); dev['require_stopped']('client')
        write(STAGE / 'installed.json', dict(installed=True, handlers=240, classes=8,
            generation='Bash + Find Item / client patch45', installedFiles={name: sha(RUNTIME / name) for name in sources},
            productionContentSha256=sha(RUNTIME / '.local/content/Sanctuary.Content.dll'),
            sourceInputsSha256=sha(QUALIFIED / 'birth-inputs.json'), originalEntitiesLoaded=5290,
            savesUnchanged=True, configurationUnchanged=True, clientSettingsDataUnchanged=True,
            productionIntegrationAbsent=True, defaultStagingVerified=True, serverStopped=True, clientStopped=True))
        print('Installed adopted Bash/Find Item and45-patch client;5290original entities load;original saves/settings preserved.')
    except BaseException as error:
        configpath.write_bytes(original_config)
        for name, digest in before.items():
            target = RUNTIME / name
            if digest is None:
                target.unlink(missing_ok=True)
            else:
                shutil.copy2(STAGE / 'rollback' / name, target); assert sha(target) == digest
        write(STAGE / 'failed-install.json', dict(error=repr(error), rollbackComplete=True))
        raise
    finally:
        configpath.write_bytes(original_config)


if __name__ == '__main__':
    install()
