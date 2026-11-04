#!/usr/bin/env python3
"""Bind the scoped fixed-point player melee death return to native regressions."""
import argparse
from collections import Counter
import json
from pathlib import Path
import subprocess
import tempfile
import shutil

import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/player-melee-return'
PARENT = '0913acc516ff766f4e611384c07b125104440391'
sha = inventory.sha


def load(path):
    return json.loads(path.read_text())


def historical(path):
    return json.loads(subprocess.check_output(['git', 'show', PARENT + ':' + path], cwd=ROOT))


def record(check=False):
    inventory.STAGE = STAGE
    paths = [STAGE / name for name in ('birth-inputs.json', 'audit-inputs.json')]
    native, audit = [inventory.validate_inventory(path) for path in paths]
    for path in paths:
        snapshot(STAGE / 'pre-build-5.json', path)
    assert native['artifacts'] == audit['artifacts'] and native['copiedData'] == audit['copiedData']
    prior_path = STAGE.with_name('necro-status-reconciliation') / 'audit-inputs.json'
    parent_receipt = historical('tools/reference/necro-status-reconciliation-evidence.json')
    assert sha(prior_path) == parent_receipt['evidence'][str(prior_path.relative_to(ROOT))]
    prior = load(prior_path)
    sources = {'src/Sanctuary.Content/' + name + '.cs' for name in
               ('PlayerMeleeReturn', 'CombatDamage', 'PaladinThorns', 'ShiverArmor')}
    sources |= {'tests/Sanctuary.Integration/' + name + '.cs' for name in
                ('PlayerMeleeReturnChecks', 'SorceressLightningChecks', 'TeleportChecks', 'MonsterReturnChecks', 'MonsterOrderedReleaseChecks')}
    assert {p for p in set(native['sourceInputs']) | set(prior['sourceInputs'])
            if p.endswith('.cs') and native['sourceInputs'].get(p) != prior['sourceInputs'].get(p)} == sources
    assert {p for p in native['copiedData'] if native['copiedData'][p] != prior['copiedData'][p]} == {
        'Data/Sanctuary/Balance/class-skill-status.json'}
    status_path = 'data/Sanctuary/Balance/class-skill-status.json'
    before, after = historical(status_path), load(ROOT / status_path)
    assert {k:v for k,v in before.items() if k != 'skills'} == {k:v for k,v in after.items() if k != 'skills'}
    changes = {a['talentId']: [k for k in a if a[k] != b[k]]
               for a,b in zip(before['skills'], after['skills'], strict=True) if a != b}
    assert changes == {'sorceress.shiverarmor': ['remaining']}
    assert len(after['skills']) == 240 and not after['completedClasses']
    assert sum(row['implementation'] == 'handler-present' for row in after['skills']) == 191
    assert not any(row['complete'] for row in after['skills'])

    proof = load(STAGE / 'native-source-evidence.json')
    parent_path = ROOT / proof['parentInventory']
    assert sha(parent_path) == proof['parentInventorySha256']
    parent = load(parent_path)
    assert parent['upstream'] == proof['parentUpstream'] == 'eff9de028136b11e544fd81375d1e0e710e22dd6'
    assert len(parent['nativeSources']) == proof['parentSourceFiles'] == 8114
    assert len(parent['patches']) == proof['parentPatches'] == 71 and proof['patches'] == 72
    old_native = STAGE.with_name('revive-native-resources') / 'native'
    delta = set()
    for path, digest in parent['nativeSources'].items():
        assert sha(old_native / path) == digest, path
        if sha(STAGE / 'native' / path) != digest:
            delta.add(path)
    assert delta == set(proof['changedSources']) == {
        'Projects/UOContent/Misc/AOS.cs', 'Projects/UOContent/Mobiles/ClassCombatHooks.cs'}
    for path, digest in proof['changedSources'].items():
        assert sha(STAGE / 'native' / path) == digest
    for path, digest in parent['patches'].items():
        assert sha(ROOT / path) == digest
    patch = ROOT / 'patches/ModernUO/0072-class-player-melee-return.patch'
    assert sha(patch) == proof['newPatchSha256'] and proof['newPatchRoundTrip']
    with tempfile.TemporaryDirectory(prefix='sanctuary-player-return-patch-') as temporary:
        folder = Path(temporary)
        for path in delta:
            (folder / path).parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(STAGE / 'native' / path, folder / path)
        for reverse in (True, False):
            subprocess.run(['git', 'apply', '--unsafe-paths', *(['--reverse'] if reverse else []), str(patch)], cwd=folder, check=True)
            expected = parent['nativeSources'] if reverse else proof['changedSources']
            assert all(sha(folder / path) == expected[path] for path in delta)
    for path, digest in proof['unchangedHostArtifacts'].items():
        assert sha(STAGE / 'native/Distribution' / path) == sha(old_native / 'Distribution' / path) == digest
    assert native['artifacts']['Assemblies/UOContent.dll'] == proof['nativeContent']
    assert sha(STAGE / 'native/Distribution/Assemblies/UOContent.dll') == proof['nativeContent']
    for path in ('ModernUO.dll', 'Server.dll'):
        assert native['artifacts'][path] == prior['artifacts'][path]

    old_log = STAGE.with_name('sorceress-shiver-armor') / 'birth-native-checks.log'
    receipt = historical('tools/reference/shiver-armor-native-evidence.json')
    assert sha(old_log) == receipt['artifacts'][str(old_log.relative_to(ROOT))]
    expected = markers(old_log)
    assert sum(expected.values()) == 484
    expected += Counter({'player-melee-return-check':1, 'player-melee-return-reload-check':2})
    assert markers(STAGE / 'birth-native-checks.log') == expected and sum(expected.values()) == 487
    smoke = STAGE.with_name('player-melee-return-smoke')
    smoked = inventory.validate_inventory(smoke / 'birth-inputs.json')
    snapshot(STAGE / 'pre-build-5.json', smoke / 'birth-inputs.json')
    assert smoked['artifacts'] == native['artifacts'] and smoked['copiedData'] == native['copiedData']
    assert sum(markers(smoke / 'birth-native-checks.log').values()) == 15
    for folder in (STAGE, smoke):
        log = (folder / 'birth-native-checks.log').read_text()
        assert ': failed' not in log and ' ERR]' not in log
        assert log.count('Shutting down') == log.count('Writing world save snapshot done') == 3
        assert markers(folder / 'birth-native-checks.log')['player-melee-return-reload-check'] == 2
        saved = load(folder / 'player-melee-return-fixtures.json')
        assert saved['Generation'] == 2 and all(saved[k] > 0 for k in ('Owner', 'Enemy', 'Life', 'EnemyLife', 'Mana'))
        assert saved['Life'] % 256 != 0 and saved['Armor'] is not None
    for name in ('native-build-2.log', 'content-build-5.log', 'integration-build-5.log'):
        assert all(value in (STAGE / name).read_text() for value in ('Build succeeded.', '0 Warning(s)', '0 Error(s)'))

    for name, count in (('named-item-runtime-audit.json', 1020), ('first-loop-affix-audit.json', 21)):
        fresh = load(STAGE / name)
        assert fresh == load(ROOT / 'tools/reference' / name) and len(fresh['InputHashes']) == count
        assert fresh['ContentAssemblySha256'] == native['artifacts']['Assemblies/Sanctuary.Content.dll']
        if 'NativeAssemblySha256' in fresh:
            assert fresh['NativeAssemblySha256'] == proof['nativeContent']
        for path, digest in fresh['InputHashes'].items():
            assert sha(ROOT / path) == digest, path
        old = historical('tools/reference/' + name)
        skip = {'InputHashes', 'ContentAssemblySha256', 'ContentAssemblyPath', 'NativeAssemblySha256'}
        assert {k:v for k,v in fresh.items() if k not in skip} == {k:v for k,v in old.items() if k not in skip}
        if 'NativeAssemblySha256' in old:
            assert old['NativeAssemblySha256'] == prior['artifacts']['Assemblies/UOContent.dll']
    assert markers(STAGE / 'audit-native-checks.log') == Counter({'named-gear-check':1, 'first-loop-catalogue-check':1})
    python = (STAGE / 'python-tests.log').read_text()
    assert 'Ran 71 tests' in python and '\nOK\n' in python and 'FAILED' not in python
    for name, text in (('readiness-check.log', '573 definitions accounted for'), ('catalogue-check.log', 'Eight classes, 240 source skills'),
                       ('roadmap-check.log', 'Class handler coverage, blockers, effects and icons recorded separately.')):
        assert text in (STAGE / name).read_text()
    client_path = ROOT / 'tools/reference/poison-corpse-aim-client-evidence.json'
    client = load(client_path)
    assert client['patches'] == 40
    for section in ('patchHashes', 'sharedInputs'):
        for path, digest in client[section].items():
            assert sha(ROOT / path) == digest, path
    installed = load(ROOT / 'tools/reference/creeper-frame-installation-evidence.json')
    assert len(installed['installedArtifacts']) == 106
    for path, digest in installed['installedArtifacts'].items():
        assert sha(Path(installed['runtimeRoot']) / path) == digest, path
    protected = load(ROOT / '.local/class-skills/creeper-frame-installation.json')['protected']
    for key, relative, count in (('saves', '.local/server/Saves', 38), ('configuration', '.local/server/Configuration', 20)):
        folder = Path(installed['runtimeRoot']) / relative
        assert len(protected[key]) == count
        assert {str(path.relative_to(folder)):sha(path) for path in folder.rglob('*') if path.is_file()} == protected[key]

    files = [Path(__file__).resolve(), patch, parent_path, prior_path, client_path, old_log,
             *(ROOT / path for path in sorted(sources)),
             ROOT / 'tests/check_player_melee_return.py', ROOT / 'tests/check_player_melee_return_smoke.py',
             ROOT / 'tools/build_skill_roadmap.py', ROOT / status_path,
             ROOT / 'src/Sanctuary.Content/SourceCastClock.cs', ROOT / 'src/Sanctuary.Content/SorceressCastClock.cs',
             ROOT / 'data/Sanctuary/Balance/sorceress-cast-clock.json',
             ROOT / '.local/class-skills/d2moo-pinned-source/source/D2Game/src/UNIT/SUnitDmg.cpp',
             STAGE / 'native/Projects/Server/Mobiles/Mobile.cs',
             *(STAGE / 'native' / path for path in sorted(delta)),
             *(ROOT / 'docs' / name for name in ('player-melee-return-checkpoint.md', 'class-skill-roadmap.md',
                 'class-skills-resume-checkpoint.md', 'class-skill-reuse-notes.md', 'skill-effects.md', 'skill-icons.md'))]
    files += [STAGE / name for name in ('pre-build-5.json', 'native-source-evidence.json', 'native-build-1.log', 'native-build-2.log',
        'content-build-5.log', 'integration-build-5.log', 'birth-inputs.json', 'birth-native-checks.log', 'native-run-5.log',
        'audit-inputs.json', 'audit-native-checks.log', 'audit-run-4.log', 'named-item-runtime-audit.json',
        'first-loop-affix-audit.json', 'python-tests.log', 'readiness-check.log', 'catalogue-check.log', 'roadmap-check.log',
        'player-melee-return-fixtures.json')]
    files += [smoke / name for name in ('birth-inputs.json', 'birth-native-checks.log', 'native-run-6.log', 'player-melee-return-fixtures.json')]
    for attempt in ('attempt-1', 'attempt-2', 'attempt-3', 'attempt-4'):
        files += [path for path in (STAGE / attempt).rglob('*') if path.is_file()]
    result = dict(schemaVersion=1, referenceVersion='D2R Reign of the Warlock 3.3.93847', parent=PARENT,
        scope='Ordinary unowned NPC fixed-point melee player HP, late reactions and retained lethal death return', installed=False,
        build=5, nativeBuild=2, fullRun=5, smokeRun=6, auditRun=4,
        verified=dict(affectedSystemMarkers=487, retainedShiverMarkers=484, focusedMarkers=15, bootsPerCohort=3,
            actualReloadsPerCohort=2, worldSavesPerCohort=3, nativePatchCount=72, checkedParentSourceFiles=8114,
            changedNativeSourceFiles=2, unchangedHostAssemblies=3, actualNativeOnHitHookDisabledControl=True,
            immediateZeroHpBeforeReaction=True, retainedLethalThroughReactionHealingAndException=True,
            originalWoundedActorsManaArmorReloads=True, pythonTests=71, auditFingerprints=[1020,21], namedDecisionsUnchanged=573,
            installedArtifactsUnchanged=106, savesUnchanged=38, configurationsUnchanged=20, clientPatchesUnchanged=40, handlers=191),
        artifacts=native['artifacts'], evidence={str(path.relative_to(ROOT)):sha(path) for path in sorted(set(files))},
        limitations=['Static older melee/player return source review plus executed scoped native adapter; not full current D2R player/world/event parity.',
            'Raw/direct native damage, ranged/spells, PvP, owned/summoned NPCs, hirelings and complete player return modes remain outside admission.',
            'The 487-marker affected-system run retains the historical 484-marker Shiver suite; not a new full Necromancer or all-class certificate.',
            'Inspection armor expiry is held only for persistence fixtures; ordinary gameplay durations and separate unheld-save/client gaps remain.',
            'Native build1 missing references and full attempts1/2/3/4 stale clock/connection/deletion fixture failures remain preserved. No production clock was changed.',
            'No deployment, save reset, new handler, class completion, final artwork or client rebuild. Effects/audio and icons remain separately tracked.'])
    encoded = json.dumps(result, indent=2) + '\n'
    target = ROOT / 'tools/reference/player-melee-return-native-evidence.json'
    if check:
        assert target.read_text() == encoded
    else:
        target.write_text(encoded)
    print('Player melee return:487 affected-system/15 focused markers, two reloads each; installed build and saves preserved.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
