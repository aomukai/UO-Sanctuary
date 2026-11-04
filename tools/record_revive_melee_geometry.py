#!/usr/bin/env python3
"""Bind live original geometry/result/contact to retained and source evidence."""
import argparse
import json
import subprocess
from pathlib import Path
import build_revive_melee_geometry as source
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/revive-melee-geometry'
FOCUS = ROOT / '.local/class-skills/revive-melee-geometry-focus'
PARENT = 'a1a9e0972d1be8abee376d8bed32be855abe100d'
sha = inventory.sha


def record(check=False):
    inventory.STAGE = STAGE
    paths = [STAGE / 'birth-inputs.json', FOCUS / 'birth-inputs.json', STAGE / 'audit-inputs.json', STAGE / 'source-native/audit-inputs.json']
    inputs = [inventory.validate_inventory(path) for path in paths]
    for path in paths:
        snapshot(STAGE / 'pre-build-4.json', path)
    tested = inputs[0]
    assert all(row['artifacts'] == tested['artifacts'] and row['copiedData'] == tested['copiedData'] for row in inputs)
    previous = ROOT / '.local/class-skills/revive-graph'
    prior = json.loads((previous / 'birth-inputs.json').read_text())
    added = 'Data/Sanctuary/Balance/revive-melee-geometry.json'
    assert {k: v for k, v in tested['copiedData'].items() if k != added} == prior['copiedData']
    assert set(tested['copiedData']) - set(prior['copiedData']) == {added}
    sources = {'src/Sanctuary.Content/' + name + '.cs' for name in ('IronGolemWorld', 'NpcMeleeResult', 'ReviveMeleeGeometry')}
    sources.add('tests/Sanctuary.Integration/ReviveMeleeGeometryChecks.cs')
    assert {p for p in set(tested['sourceInputs']) | set(prior['sourceInputs'])
            if p.endswith('.cs') and tested['sourceInputs'].get(p) != prior['sourceInputs'].get(p)} == sources
    for name in ('ModernUO.dll', 'Server.dll', 'Assemblies/UOContent.dll'):
        assert tested['artifacts'][name] == prior['artifacts'][name] == sha(STAGE / 'native/Distribution' / name)
    parent = json.loads(subprocess.check_output(['git', 'show', PARENT + ':tools/reference/revive-graph-native-evidence.json'], cwd=ROOT))
    for folder, old_folder, count, retained in ((STAGE, previous, 520, 517), (FOCUS, previous.with_name('revive-graph-focus'), 45, 42)):
        old = old_folder / 'birth-native-checks.log'
        assert sha(old) == parent['evidence'][str(old.relative_to(ROOT))]
        expected = markers(old)
        assert sum(expected.values()) == retained
        expected['revive-melee-geometry-check'] += 1
        expected['revive-melee-geometry-reload-check'] += 2
        assert markers(folder / 'birth-native-checks.log') == expected and sum(expected.values()) == count
        log = (folder / 'birth-native-checks.log').read_text()
        assert ': failed' not in log and ' ERR]' not in log
        assert log.count('Shutting down') == log.count('Writing world save snapshot done') == 3
        saved = json.loads((folder / 'revive-melee-geometry-fixtures.json').read_text())
        assert saved['Generation'] == 2 and all(saved[k] > 0 for k in ('Owner', 'Actor', 'Life', 'Seed'))
        assert saved['Owner'] != saved['Actor']
    for kind in ('content', 'integration'):
        assert all(t in (STAGE / (kind + '-build-4.log')).read_text() for t in ('Build succeeded.', '0 Warning(s)', '0 Error(s)'))
    source.build(check=True)
    geometry = json.loads((ROOT / 'tools/reference/melee-geometry-source-evidence.json').read_text())
    result_source = json.loads((ROOT / 'tools/reference/melee-result-source-evidence.json').read_text())
    probe = '.local/class-skills/melee-geometry-source-probe.json'
    for path, digest in ((probe, geometry['artifacts'][probe]), (result_source['fixture']['path'], result_source['fixture']['sha256'])):
        assert sha(ROOT / path) == digest == inputs[3]['sourceInputs'][path]
    source_log = (STAGE / 'source-native/audit-native-checks.log').read_text()
    assert ': failed' not in source_log and ' ERR]' not in source_log and source_log.count(': passed') == 4
    assert all(t in source_log for t in ('32400distance/endpoints,91464range and14400retained wolf distances', '16384complete-source flag/ordered-service comparisons and4actual Iron world'))
    assert (STAGE / 'audit-native-checks.log').read_text().count(': passed') == 2
    for name, count in (('named-item-runtime-audit.json', 1009), ('first-loop-affix-audit.json', 21)):
        fresh = json.loads((STAGE / name).read_text())
        assert fresh == json.loads((ROOT / 'tools/reference' / name).read_text()) and len(fresh['InputHashes']) == count
        assert fresh['ContentAssemblySha256'] == tested['artifacts']['Assemblies/Sanctuary.Content.dll']
        for path, digest in fresh['InputHashes'].items():
            assert sha(ROOT / path) == digest, path
        other = json.loads((STAGE / 'source-native' / name).read_text())
        assert {k: v for k, v in fresh.items() if k != 'ContentAssemblyPath'} == {k: v for k, v in other.items() if k != 'ContentAssemblyPath'}
        old = json.loads(subprocess.check_output(['git', 'show', PARENT + ':tools/reference/' + name], cwd=ROOT))
        skip = {'InputHashes', 'ContentAssemblySha256', 'ContentAssemblyPath'}
        assert {k: v for k, v in fresh.items() if k not in skip} == {k: v for k, v in old.items() if k not in skip}
    assert 'Ran 71 tests' in (STAGE / 'python-tests.log').read_text() and '\nOK\n' in (STAGE / 'python-tests.log').read_text()
    for name, text in (('readiness-check.log', '573 definitions accounted for'), ('catalogue-check.log', 'Eight classes, 240 source skills'),
                       ('roadmap-check.log', 'Class handler coverage, blockers, effects and icons recorded separately.')):
        assert text in (STAGE / name).read_text()
    installed = json.loads((ROOT / 'tools/reference/creeper-frame-installation-evidence.json').read_text())
    assert len(installed['installedArtifacts']) == 106
    for path, digest in installed['installedArtifacts'].items():
        assert sha(Path(installed['runtimeRoot']) / path) == digest, path
    protected = json.loads((ROOT / '.local/class-skills/creeper-frame-installation.json').read_text())['protected']
    for key, relative, count in (('saves', '.local/server/Saves', 38), ('configuration', '.local/server/Configuration', 20)):
        folder = Path(installed['runtimeRoot']) / relative
        assert len(protected[key]) == count and {str(p.relative_to(folder)): sha(p) for p in folder.rglob('*') if p.is_file()} == protected[key]
    status = json.loads((ROOT / 'data/Sanctuary/Balance/class-skill-status.json').read_text())
    assert not status['completedClasses'] and sum(s['implementation'] == 'handler-present' for s in status['skills']) == 191
    files = [Path(__file__).resolve(), *(ROOT / p for p in sorted(sources)), ROOT / 'tools/build_revive_melee_geometry.py',
             ROOT / 'data/Sanctuary/Balance/revive-melee-geometry.json', ROOT / 'tools/reference/revive-melee-geometry-source-evidence.json',
             ROOT / 'tools/reference/melee-geometry-source-evidence.json', ROOT / 'tools/reference/melee-result-source-evidence.json',
             *(ROOT / 'tests' / name for name in ('check_revive_melee_geometry.py', 'check_revive_melee_geometry_focus.py')),
             *(ROOT / 'docs' / name for name in ('revive-melee-geometry-checkpoint.md', 'class-skill-roadmap.md', 'class-skills-resume-checkpoint.md', 'class-skill-reuse-notes.md', 'skill-effects.md', 'skill-icons.md')),
             *(STAGE / name for name in ('pre-build-4.json', 'birth-inputs.json', 'birth-native-checks.log', 'native-run-2.log', 'audit-inputs.json', 'audit-native-checks.log', 'audit-run-2.log',
               'content-build-4.log', 'integration-build-1.log', 'integration-build-4.log', 'python-tests.log', 'readiness-check.log', 'catalogue-check.log', 'roadmap-check.log', 'source-check.log', 'revive-melee-geometry-fixtures.json')),
             *(FOCUS / name for name in ('birth-inputs.json', 'birth-native-checks.log', 'native-run-3.log', 'revive-melee-geometry-fixtures.json', 'failed-1/birth-native-checks.log')),
             *(STAGE / 'source-native' / name for name in ('audit-inputs.json', 'audit-native-checks.log', 'run-2.log')),
             *(STAGE / 'build-3-native' / name for name in ('birth-inputs.json', 'birth-native-checks.log', 'Sanctuary.Integration.dll'))]
    result = dict(schemaVersion=1, referenceVersion='D2R Reign of the Warlock 3.3.93847', parent=PARENT,
        scope='Live retained-source geometry, shared NPC hit/block binding and actual ordered contact; native swing scheduling/public equipped casting unchanged',
        verified=dict(nativeMarkers=520, focusedMarkers=45, parentMarkersRetained=517, bootsPerCohort=3, actualReloadsPerCohort=2,
            sourceRows=751, sourceGeometryCases=32400, sourceRangeCases=91464, retainedWolfDistances=14400, sourceResultCases=16384, actualIronWorldControls=4,
            actualOriginalResultScenes=6, sourceBlockControl=True, nativeWallClearBlockedClear=True, successfulAndEscapedContact=True,
            exactHitStreamDraws=True, exactNextDrawAfterReload=True, partialWoundsReloaded=True, unknownIdentityPreDrawRefusal=True,
            pythonTests=71, auditFingerprints=[1009, 21], namedDecisionsUnchanged=573, installedArtifactsUnchanged=106, savesUnchanged=38, configurationsUnchanged=20, handlers=191),
        artifacts=tested['artifacts'], evidence={str(p.relative_to(ROOT)): sha(p) for p in files},
        limitations=['Native swing timing, pending actions/cancellation/persistence and public equipped casting remain unfinished.',
            'Britannia facet/height and shrunk-endpoint native LOS are explicit adapters, not D2 collision-grid/room or current-executable parity.',
            'Current event subscription history, component/block RNG ownership, unsupported item/hand/element/AI/world/PvP/client cases remain explicit.',
            'No class completion, deployment or final artwork; effects/audio and talent/hotkey icons remain separate.'])
    encoded = json.dumps(result, indent=2) + '\n'
    target = ROOT / 'tools/reference/revive-melee-geometry-native-evidence.json'
    if check:
        assert target.read_text() == encoded
    else:
        target.write_text(encoded)
    print('Revive live melee:520 broader/45 focused;517 parent markers,751 owned rows,source suites,actual wall/block/contact and two reloads;installation preserved.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
