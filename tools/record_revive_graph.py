#!/usr/bin/env python3
"""Bind frozen original-item ownership and suspended native resource evidence."""
import argparse
import json
import subprocess
from pathlib import Path
import record_grizzly_body as inventory
import prepare_revive_resource_native as prepared
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/revive-graph'
FOCUS = ROOT / '.local/class-skills/revive-graph-focus'
PARENT = '511838831d9c242e6413f2bf9e84ba3d8e32732c'
sha = inventory.sha


def record(check=False):
    inventory.STAGE = STAGE
    tested = inventory.validate_inventory(STAGE / 'birth-inputs.json')
    focus = inventory.validate_inventory(FOCUS / 'birth-inputs.json')
    audit = inventory.validate_inventory(STAGE / 'audit-inputs.json')
    for folder, name in ((STAGE, 'birth-inputs.json'), (FOCUS, 'birth-inputs.json'), (STAGE, 'audit-inputs.json')):
        snapshot(STAGE / 'pre-build-3.json', folder / name)
    assert tested['artifacts'] == focus['artifacts'] == audit['artifacts']
    assert tested['copiedData'] == focus['copiedData'] == audit['copiedData']
    previous = ROOT / '.local/class-skills/revive-incoming'
    prior = json.loads((previous / 'birth-inputs.json').read_text())
    assert tested['copiedData'] == prior['copiedData']
    sources = {'src/Sanctuary.Content/' + name + '.cs' for name in (
        'ClassStats', 'FrozenEquipmentCombat', 'FrozenEquipmentEffects', 'FrozenNpcStatGraph',
        'IronGolemBodyNumbers', 'ItemStatContext', 'NpcEquipmentCombat', 'NpcItemStatCapture',
        'ReviveEquipmentCombat', 'ReviveEquipmentEvents', 'ReviveEquipmentGraph',
        'ReviveEquipmentLife', 'ReviveEquipmentResources', 'ReviveNativeActivation')}
    sources.add('tests/Sanctuary.Integration/ReviveGraphChecks.cs')
    changed = {p for p in set(tested['sourceInputs']) | set(prior['sourceInputs'])
               if p.endswith('.cs') and tested['sourceInputs'].get(p) != prior['sourceInputs'].get(p)}
    assert changed == sources
    parent = json.loads(subprocess.check_output(
        ['git', 'show', PARENT + ':tools/reference/revive-incoming-native-evidence.json'], cwd=ROOT))
    for folder, old_folder, total, parent_total in (
        (STAGE, previous, 517, 514), (FOCUS, previous.with_name('revive-incoming-focus'), 42, 39)):
        old = old_folder / 'birth-native-checks.log'
        assert sha(old) == parent['evidence'][str(old.relative_to(ROOT))]
        expected = markers(old)
        assert sum(expected.values()) == parent_total
        expected['revive-graph-check'] += 1
        expected['revive-graph-reload-check'] += 2
        assert markers(folder / 'birth-native-checks.log') == expected and sum(expected.values()) == total
        log = (folder / 'birth-native-checks.log').read_text()
        assert ': failed' not in log and ' ERR]' not in log
        assert log.count('Shutting down') == log.count('Writing world save snapshot done') == 3
        saved = json.loads((folder / 'revive-graph-fixtures.json').read_text())
        assert saved['Generation'] == 2 and saved['Owner'] > 0
        assert [actor['Graph'] for actor in saved['Actors']] == [True, True, False]
        assert len({actor['Actor'] for actor in saved['Actors']}) == 3
        for actor in saved['Actors']:
            assert actor['Actor'] > 0 and len(actor['Expected']) == 99
            assert all(len(row) == 13 for row in actor['Expected'])
        for pool in ('Life', 'Mana', 'Stamina'):
            assert len(saved[pool]) == 3 and all(value > 0 for value in saved[pool])
    for kind, number in (('native', 2), ('content', 3), ('integration', 3)):
        assert all(t in (STAGE / f'{kind}-build-{number}.log').read_text()
                   for t in ('Build succeeded.', '0 Warning(s)', '0 Error(s)'))
    native = prepared.native.STAGE / 'native'
    manifest_path = prepared.native.STAGE / 'native-pre-build-inputs.json'
    manifest = json.loads(manifest_path.read_text())
    assert manifest == prepared.native.inputs(native)
    assert manifest['upstream'] == prepared.native.PIN and len(manifest['patches']) == 71
    old_native = previous / 'native'
    native_changed = {name for name, digest in manifest['nativeSources'].items()
                      if not (old_native / name).is_file() or sha(old_native / name) != digest}
    assert native_changed == {'Projects/UOContent/Items/ClassGearHooks.cs', 'Projects/UOContent/Mobiles/BaseCreature.cs'}
    for name in ('ModernUO.dll', 'Server.dll', 'Assemblies/UOContent.dll'):
        assert sha(native / 'Distribution' / name) == tested['artifacts'][name]
    for name, count in (('named-item-runtime-audit.json', 1006), ('first-loop-affix-audit.json', 21)):
        fresh = json.loads((STAGE / name).read_text())
        assert fresh == json.loads((ROOT / 'tools/reference' / name).read_text())
        assert len(fresh['InputHashes']) == count
        assert fresh['ContentAssemblySha256'] == tested['artifacts']['Assemblies/Sanctuary.Content.dll']
        if name == 'named-item-runtime-audit.json':
            assert fresh['NativeAssemblySha256'] == tested['artifacts']['Assemblies/UOContent.dll']
        for path, digest in fresh['InputHashes'].items():
            assert sha(ROOT / path) == digest, path
        old = json.loads(subprocess.check_output(['git', 'show', PARENT + ':tools/reference/' + name], cwd=ROOT))
        skip = {'InputHashes', 'ContentAssemblySha256', 'ContentAssemblyPath', 'NativeAssemblySha256'}
        assert {k: v for k, v in fresh.items() if k not in skip} == {k: v for k, v in old.items() if k not in skip}
    assert (STAGE / 'audit-native-checks.log').read_text().count(': passed') == 2
    assert 'Ran 71 tests' in (STAGE / 'python-tests.log').read_text()
    assert '\nOK\n' in (STAGE / 'python-tests.log').read_text()
    for name, text in (('readiness-check.log', '573 definitions accounted for'),
                       ('catalogue-check.log', 'Eight classes, 240 source skills'),
                       ('roadmap-check.log', 'Class handler coverage, blockers, effects and icons recorded separately.')):
        assert text in (STAGE / name).read_text()
    installed = json.loads((ROOT / 'tools/reference/creeper-frame-installation-evidence.json').read_text())
    assert len(installed['installedArtifacts']) == 106
    for path, digest in installed['installedArtifacts'].items():
        assert sha(Path(installed['runtimeRoot']) / path) == digest, path
    protected = json.loads((ROOT / '.local/class-skills/creeper-frame-installation.json').read_text())['protected']
    for key, relative, count in (('saves', '.local/server/Saves', 38), ('configuration', '.local/server/Configuration', 20)):
        folder = Path(installed['runtimeRoot']) / relative
        assert len(protected[key]) == count
        assert {str(p.relative_to(folder)): sha(p) for p in folder.rglob('*') if p.is_file()} == protected[key]
    status = json.loads((ROOT / 'data/Sanctuary/Balance/class-skill-status.json').read_text())
    assert not status['completedClasses'] and sum(s['implementation'] == 'handler-present' for s in status['skills']) == 191
    files = [Path(__file__).resolve(), manifest_path, *(ROOT / p for p in sorted(sources)),
             ROOT / 'tools/prepare_revive_resource_native.py', ROOT / 'tools/prepare_revive_retention_native.py',
             ROOT / 'tools/reference/defiler-user-observations.json',
             *(ROOT / p for p in manifest['patches']),
             *(native / 'Distribution' / name for name in ('ModernUO.dll', 'Server.dll', 'Assemblies/UOContent.dll')),
             *(ROOT / 'tests' / name for name in ('check_revive_graph.py', 'check_revive_graph_focus.py')),
             *(ROOT / 'docs' / name for name in ('revive-graph-checkpoint.md', 'defiler-compiled-formula-checkpoint.md',
               'class-skill-roadmap.md', 'class-skills-resume-checkpoint.md', 'class-skill-reuse-notes.md', 'skill-effects.md', 'skill-icons.md')),
             *(STAGE / name for name in ('pre-build-3.json', 'birth-inputs.json', 'birth-native-checks.log', 'native-run-1.log',
               'audit-inputs.json', 'audit-native-checks.log', 'audit-run-1.log', 'native-build-1.log', 'native-build-2.log',
               'native-prepare-1.log', 'native-prepare-2.log', 'native-prepare-check.log', 'content-build-3.log',
               'integration-build-3.log', 'python-tests.log', 'readiness-check.log', 'catalogue-check.log',
               'roadmap-check.log', 'revive-graph-fixtures.json')),
             *(FOCUS / name for name in ('birth-inputs.json', 'birth-native-checks.log', 'native-run-4.log', 'revive-graph-fixtures.json')),
             *(FOCUS / f'failed-{n}' / 'birth-native-checks.log' for n in (1, 2, 3))]
    result = dict(schemaVersion=1, referenceVersion='D2R Reign of the Warlock 3.3.93847', parent=PARENT,
        scope='Opt-in frozen item ownership/resource graph and suspended native mana/stamina recovery gates; public equipped casting unchanged',
        verified=dict(nativeMarkers=517, focusedMarkers=42, parentMarkersRetained=514, bootsPerCohort=3,
            actualReloadsPerCohort=2, savedOriginalsPerCohort=3, graphProfilesPerCohort=2, legacyControlsPerCohort=1,
            liveSupplierLevels=99, supplierFieldsPerLevel=13, explicitProjectionLevels=[1, 3, 21, 99],
            forcedNativeTimerTicksPerActorPerBoot=12, unmanagedRecoveryControl=True, exactPartialResourceReloads=True,
            historicalMissingBasesRefused=True, unknownProfileAtomicRefusal=True, worldTimeServiceRefusal=True,
            nativePatchCount=71, nativeSourceFilesChanged=2, pythonTests=71, auditFingerprints=[1006, 21],
            namedDecisionsUnchanged=573, installedArtifactsUnchanged=106, savesUnchanged=38, configurationsUnchanged=20, handlers=191),
        artifacts=tested['artifacts'], evidence={str(p.relative_to(ROOT)): sha(p) for p in files},
        limitations=['Graph construction and canonical registry order are not recovered current-world event subscription history.',
            'Original native geometry/result/controller scheduling and pending action persistence remain unfinished; public equipped casts stay gated.',
            'World-time coefficients, missing historical graph bases and unadmitted raw properties/bodies/hand/element/AI/world/PvP/client cases remain explicit.',
            'Defiler observations establish link/follow support behavior, not a damage formula or complete Health Link implementation.',
            'No completed class, deployment or final artwork; effects/audio and talent/hotkey icons remain separate.'])
    encoded = json.dumps(result, indent=2) + '\n'
    target = ROOT / 'tools/reference/revive-graph-native-evidence.json'
    if check:
        assert target.read_text() == encoded
    else:
        target.write_text(encoded)
    print('Original Revive graph:517 broader/42 focused markers,514 parent retained;71 patches,exact wounded reloads/native timer controls;installation preserved.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
