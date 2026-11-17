#!/usr/bin/env python3
"""Promote the verified class package, retaining original saves and rollback files."""
import argparse
import json
from pathlib import Path
import runpy
import shutil
import subprocess
import sys
from prepare_class_skills_handoff import hashes, protected, selected_files, sha, write

ROOT = Path(__file__).resolve().parents[1]
WORLD = ROOT / '.local/class-skills/class-skills-handoff'
CLIENT = ROOT / '.local/class-skills/class-skills-client-package'
MERGE = ROOT / '.local/class-skills/main-consolidation'


def install(runtime, stage):
    assert not stage.exists(), 'Keep each install attempt and rollback separate'
    prepared = json.loads((WORLD / 'prepared-inputs.json').read_text())
    world = json.loads((WORLD / 'world-smoke.json').read_text())
    client = json.loads((CLIENT / 'client-startup.json').read_text())
    comparison = json.loads((MERGE / 'source-comparison-2.json').read_text())
    assert comparison['nativeMismatches'] == comparison['clientMismatches'] == []
    assert world['identitiesPreserved'] and client['aliveAfterRender']
    assert protected(runtime) == prepared['original']
    assert selected_files(runtime) == prepared['originalFiles']
    assert hashes(runtime / 'data/Sanctuary') == hashes(WORLD / 'server/Data/Sanctuary')
    dev = runpy.run_path(str(runtime / 'dev'))
    dev['require_stopped']('server')
    dev['require_stopped']('client')
    dev['require_sdk']()
    stage.mkdir(parents=True)
    sources = {}

    def add(source, destination):
        assert source.is_file() and destination.is_relative_to(runtime)
        assert not destination.is_symlink(), destination
        if destination.exists() and sha(source) == sha(destination):
            return
        sources[str(destination.relative_to(runtime))] = source

    for path, digest in prepared['candidateServer'].items():
        source = WORLD / 'server' / path
        assert sha(source) == digest, path
        add(source, runtime / '.local/server' / path)
    add(WORLD / 'server/Assemblies/Sanctuary.Content.dll', runtime / '.local/content/Sanctuary.Content.dll')
    # Native reference assemblies used by an ordinary content rebuild must
    # match the installed API, without rebuilding the already verified server.
    native = (ROOT / '.local/class-skills/warlock-lifecycle-acceptance/native/Distribution').resolve()
    for folder in (native, native / 'Assemblies'):
        for path in folder.iterdir():
            if path.is_file() and path.suffix in ('.dll', '.json', '.config'):
                add(path, runtime / 'upstream/ModernUO/Distribution' / path.relative_to(native))
    # Keep the normal client test/build output consistent with the package.
    for path in (CLIENT / 'client').iterdir():
        if path.is_file() and path.suffix not in ('.pdb', '.nupkg') and path.name != 'settings.json':
            add(path, runtime / '.local/client' / path.name)
            add(path, runtime / 'upstream/ClassicUO/bin/Debug/net10.0' / path.name)
    integration = ROOT / '.local/class-skills/warlock-lifecycle-acceptance/integration/Sanctuary.Integration.dll'
    add(integration, runtime / '.local/integration/Sanctuary.Integration.dll')
    expected_data = hashes(WORLD / 'server/Data/Sanctuary')
    remove = [str(path.relative_to(runtime)) for path in (runtime / '.local/server/Data/Sanctuary').rglob('*')
              if path.is_file() and str(path.relative_to(runtime / '.local/server/Data/Sanctuary')) not in expected_data]
    before = {}
    for name in sorted(set(sources) | set(remove)):
        path = runtime / name
        before[name] = sha(path) if path.exists() else None
        if path.exists():
            backup = stage / 'rollback' / name
            backup.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(path, backup)
            assert sha(backup) == before[name]
    planned = {name: dict(source=str(source), sha256=sha(source)) for name, source in sources.items()}
    write(stage / 'plan.json', dict(runtimeRoot=str(runtime), files=planned, remove=remove,
          previousFiles=before, originalSaves=prepared['original']['.local/server/Saves'],
          originalConfiguration=prepared['original']['.local/server/Configuration']))
    configuration = runtime / '.local/server/Configuration/modernuo.json'
    original_config = configuration.read_bytes()
    installed = []
    try:
        for name, source in sources.items():
            target = runtime / name
            target.parent.mkdir(parents=True, exist_ok=True)
            pending = target.with_name(target.name + '.class-skills-pending')
            assert not pending.exists()
            shutil.copy2(source, pending)
            assert sha(pending) == planned[name]['sha256']
            pending.replace(target)
            installed.append(name)
        for name in remove:
            (runtime / name).unlink()
        assert hashes(runtime / '.local/server/Data/Sanctuary') == expected_data
        assert json.loads((runtime / '.local/server/Data/assemblies.json').read_text()) == ['UOContent.dll', 'Sanctuary.Content.dll']
        assert not (runtime / '.local/server/Assemblies/Sanctuary.Integration.dll').exists()
        config = json.loads(original_config)
        config['settings'].update({'autosave.enabled': 'False', 'crashGuard.restartServer': 'False'})
        configuration.write_text(json.dumps(config))
        sys.path.insert(0, str(ROOT / 'tests'))
        from check_britain import Session
        with (stage / 'installed-world-smoke.log').open('w') as log:
            session = Session(runtime / '.local/server', log)
            try:
                session.expect(r'Listening: 127.0.0.1:2593')
                session.expect(r'Listening: .*:12000')
                session.send('shutdown')
                session.expect('Shutting down')
                assert session.process.wait(timeout=20) == 0
            finally:
                session.close()
        configuration.write_bytes(original_config)
        log = (stage / 'installed-world-smoke.log').read_text()
        assert 'Loading world done (4698 items, 572 mobiles)' in log
        assert ' ERR]' not in log and 'create the owner account now?' not in log and 'Saving world' not in log
        for name in ('.local/server/Saves', '.local/server/Configuration', '.local/client/Profiles', '.local/client/Data'):
            assert hashes(runtime / name) == prepared['original'][name], name
        assert sha(runtime / '.local/client/settings.json') == prepared['originalFiles']['.local/client/settings.json']
        for name, row in planned.items():
            assert sha(runtime / name) == row['sha256'], name
        dev['require_stopped']('server')
        dev['require_stopped']('client')
        # Check the default launch staging path with synchronized source/cache.
        dev['stage_content']()
        assert hashes(runtime / '.local/server/Data/Sanctuary') == expected_data
        assert sha(runtime / '.local/server/Assemblies/Sanctuary.Content.dll') == sha(runtime / '.local/content/Sanctuary.Content.dll')
        write(stage / 'installed.json', dict(installed=True, handlers=240, classes=8,
              runtimeRoot=str(runtime), installedFiles={name: sha(runtime / name) for name in sources},
              removedObsoleteData=remove, sourceCommit=subprocess.check_output(
                  ['git', 'rev-parse', 'HEAD'], cwd=runtime, text=True).strip(),
              originalSaveFiles=38, originalConfigurationFiles=20, savesUnchanged=True,
              configurationUnchanged=True, clientSettingsDataUnchanged=True, installedWorldLoaded=True,
              installedWorldSaved=False, serverStopped=True, clientStopped=True,
              defaultStagingVerified=True, rollbackRoot=str(stage / 'rollback'),
              graphicalGameplayVerified=False, goalComplete=False))
        print('Installed240-handler package;original5290entities load,no world save;38saves/20configs/client settings unchanged;default staging synchronized;server stopped.')
    except BaseException as error:
        write(stage / 'failed-install.json', dict(error=repr(error), installedBeforeFailure=installed,
              sourceFiles=planned, rollbackRequired=True))
        dev['require_stopped']('server')
        for name, digest in before.items():
            target = runtime / name
            if digest is None:
                if target.exists():
                    target.unlink()
            else:
                target.parent.mkdir(parents=True, exist_ok=True)
                shutil.copy2(stage / 'rollback' / name, target)
                assert sha(target) == digest
        for name in ('Saves', 'Configuration'):
            target = runtime / '.local/server' / name
            if hashes(target) != prepared['original']['.local/server/' + name]:
                shutil.rmtree(target)
                shutil.copytree(WORLD / 'original/.local/server' / name, target)
        write(stage / 'rollback-complete.json', dict(restoredFiles=len(before), savesRestored=True))
        raise
    finally:
        configuration.write_bytes(original_config)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--runtime-root', type=Path, default=Path('/media/aomukai/FILES/Sanctuary'))
    parser.add_argument('--stage-root', type=Path, required=True)
    args = parser.parse_args()
    install(args.runtime_root.resolve(), args.stage_root.resolve())
