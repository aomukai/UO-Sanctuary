#!/usr/bin/env python3
"""Bind ordinary Necromancer town guards to native inputs, reloads and protections."""
import argparse
import json
import subprocess
from pathlib import Path

import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/necro-town'
PARENT = '19acf931627038d59ab08a893a54a12493ebd82e'
sha = inventory.sha


def record(check=False):
    inventory.STAGE = STAGE
    paths = [STAGE / name for name in ('birth-inputs.json', 'audit-inputs.json')]
    native, audit = [inventory.validate_inventory(path) for path in paths]
    for path in paths:
        snapshot(STAGE / 'pre-build-2.json', path)
    assert native['artifacts'] == audit['artifacts'] and native['copiedData'] == audit['copiedData']
    previous = STAGE.with_name('corpse-aim')
    prior = json.loads((previous / 'birth-inputs.json').read_text())
    sources = {'src/Sanctuary.Content/' + name + '.cs' for name in (
        'NecromancerCastRegions', 'NecromancerCombat', 'ClassCombat', 'SkeletonPets', 'CorpseExplosionCasts')}
    sources.add('tests/Sanctuary.Integration/NecromancerTownChecks.cs')
    assert {path for path in set(native['sourceInputs']) | set(prior['sourceInputs'])
            if path.endswith('.cs') and native['sourceInputs'].get(path) != prior['sourceInputs'].get(path)} == sources
    assert native['copiedData'] == prior['copiedData']
    for name in ('ModernUO.dll', 'Server.dll', 'Assemblies/UOContent.dll'):
        assert native['artifacts'][name] == prior['artifacts'][name] == sha(STAGE / 'native/Distribution' / name)

    parent = json.loads(subprocess.check_output([
        'git', 'show', PARENT + ':tools/reference/corpse-aim-native-evidence.json'], cwd=ROOT))
    old_log = previous / 'birth-native-checks.log'
    assert sha(old_log) == parent['evidence'][str(old_log.relative_to(ROOT))]
    expected = markers(old_log)
    assert sum(expected.values()) == 78
    expected['necro-town-check'] += 1
    expected['necro-town-reload-check'] += 2
    for command in ('necro-town-right', 'necro-town-quick', 'necro-town-armor-quick'):
        expected[command + '-check'] += 3
        expected[command + '-result'] += 3
    assert sum(expected.values()) == 99
    assert markers(STAGE / 'birth-native-checks.log') == expected
    smoke = STAGE.with_name('necro-town-smoke')
    smoked = inventory.validate_inventory(smoke / 'birth-inputs.json')
    snapshot(STAGE / 'pre-build-2.json', smoke / 'birth-inputs.json')
    assert smoked['artifacts'] == native['artifacts'] and smoked['copiedData'] == native['copiedData']
    assert sum(markers(smoke / 'birth-native-checks.log').values()) == 30
    for folder in (STAGE, smoke):
        log = (folder / 'birth-native-checks.log').read_text()
        assert ': failed' not in log and ' ERR]' not in log
        assert log.count('Shutting down') == log.count('Writing world save snapshot done') == 3
        saved = json.loads((folder / 'necro-town-fixtures.json').read_text())
        assert saved['Generation'] == 2 and all(saved[key] > 0 for key in ('Owner', 'Golem', 'Material', 'Life', 'Mana'))
        for command in ('necro-town-right', 'necro-town-quick', 'necro-town-armor-quick'):
            assert log.count(command + '-result: passed') == 3

    reference_path = ROOT / 'tools/reference/necromancer-town-reference.json'
    reference = json.loads(reference_path.read_text())
    assert len(reference['rules']) == 30
    assert sum(row['inTown'] for row in reference['rules']) == 5
    assert sum(not row['passive'] and not row['inTown'] for row in reference['rules']) == 22
    assert sum(row['passive'] for row in reference['rules']) == 3
    for path, digest in reference['inputs'].items():
        assert sha(ROOT / path) == digest, path
    for kind in ('content', 'integration'):
        build = (STAGE / (kind + '-build-2.log')).read_text()
        assert all(text in build for text in ('Build succeeded.', '0 Warning(s)', '0 Error(s)'))
    for name, count in (('named-item-runtime-audit.json', 1016), ('first-loop-affix-audit.json', 21)):
        fresh = json.loads((STAGE / name).read_text())
        assert fresh == json.loads((ROOT / 'tools/reference' / name).read_text())
        assert len(fresh['InputHashes']) == count
        assert fresh['ContentAssemblySha256'] == native['artifacts']['Assemblies/Sanctuary.Content.dll']
        for path, digest in fresh['InputHashes'].items():
            assert sha(ROOT / path) == digest, path
        old = json.loads(subprocess.check_output(['git', 'show', PARENT + ':tools/reference/' + name], cwd=ROOT))
        skip = {'InputHashes', 'ContentAssemblySha256', 'ContentAssemblyPath'}
        assert {key: value for key, value in fresh.items() if key not in skip} == {
            key: value for key, value in old.items() if key not in skip}
    assert (STAGE / 'audit-native-checks.log').read_text().count(': passed') == 2
    python = (STAGE / 'python-tests.log').read_text()
    assert 'Ran 71 tests' in python and '\nOK\n' in python and 'FAILED' not in python
    for name, text in (
        ('readiness-check.log', '573 definitions accounted for'),
        ('catalogue-check.log', 'Eight classes, 240 source skills'),
        ('roadmap-check.log', 'Class handler coverage, blockers, effects and icons recorded separately.')):
        assert text in (STAGE / name).read_text()
    status = json.loads((ROOT / 'data/Sanctuary/Balance/class-skill-status.json').read_text())
    assert not status['completedClasses'] and sum(row['implementation'] == 'handler-present' for row in status['skills']) == 191

    installed = json.loads((ROOT / 'tools/reference/creeper-frame-installation-evidence.json').read_text())
    assert len(installed['installedArtifacts']) == 106
    for path, digest in installed['installedArtifacts'].items():
        assert sha(Path(installed['runtimeRoot']) / path) == digest, path
    protected = json.loads((ROOT / '.local/class-skills/creeper-frame-installation.json').read_text())['protected']
    for key, relative, count in (('saves', '.local/server/Saves', 38), ('configuration', '.local/server/Configuration', 20)):
        folder = Path(installed['runtimeRoot']) / relative
        assert len(protected[key]) == count
        assert {str(path.relative_to(folder)): sha(path) for path in folder.rglob('*') if path.is_file()} == protected[key]

    files = [Path(__file__).resolve(), *(ROOT / path for path in sorted(sources)), reference_path,
             ROOT / 'tools/review_necromancer_towns.py', ROOT / 'tests/check_necro_town.py',
             ROOT / 'tests/check_necro_town_smoke.py', ROOT / 'tests/check_corpse_aim.py',
             ROOT / 'tools/build_skill_roadmap.py', ROOT / 'data/Sanctuary/Balance/class-skill-status.json',
             *(ROOT / 'docs' / name for name in ('necro-town-checkpoint.md', 'class-skill-roadmap.md',
                'class-skills-resume-checkpoint.md', 'class-skill-reuse-notes.md', 'skill-effects.md', 'skill-icons.md')),
             *(STAGE / name for name in ('pre-build-1.json', 'pre-build-2.json', 'birth-inputs.json',
                'birth-native-checks.log', 'native-run-1.log', 'audit-inputs.json', 'audit-native-checks.log',
                'audit-run-1.log', 'content-build-1.log', 'content-build-2.log', 'integration-build-2.log',
                'python-tests.log', 'readiness-check.log', 'catalogue-check.log', 'roadmap-check.log',
                'necro-town-fixtures.json')),
             *(smoke / name for name in ('birth-inputs.json', 'birth-native-checks.log',
                'native-run-1.log', 'necro-town-fixtures.json'))]
    result = dict(schemaVersion=1, referenceVersion='D2R Reign of the Warlock 3.3.93847', parent=PARENT,
        scope='Ordinary learned Necromancer caster-town permissions;shared region guard and existing allowed-summon parking',
        verified=dict(affectedSystemNativeMarkers=99, parentMarkersRetained=78, smokeMarkers=30,
            bootsPerCohort=3, actualReloadsPerCohort=2, actualWorldSavesPerCohort=3,
            sourceRows=30, townEnabledActives=5, townForbiddenActives=22, passives=3,
            deniedRightInputsPerCohort=3, deniedQuickInputsPerCohort=3, allowedArmorQuickInputsPerCohort=3,
            forbiddenStartsNoAnimationPaymentOrConsumption=True, regionRecheckedAtRelease=True,
            fieldRaiseSkeletonRetained=True, fiveAllowedTownSpells=True, allFourGolemsParked=True,
            originalIronMaterialWoundsManaAndArmorReloaded=True, runtimeDataProtocolAndKernelUnchanged=True,
            pythonTests=71, auditFingerprints=[1016, 21], namedDecisionsUnchanged=573,
            installedArtifactsUnchanged=106, savesUnchanged=38, configurationsUnchanged=20, handlers=191),
        artifacts=native['artifacts'], evidence={str(path.relative_to(ROOT)): sha(path) for path in files},
        limitations=[
            'Owned current skill-table identity plus static older-source review;not a newly executed full source/current executable dispatcher certificate.',
            'Ordinary learned-player begin/validation/release only. Used-skill flag1 exceptions,forced casts,charges,procs,NPC execution and complete region parity remain unverified.',
            'Actual server wire fixtures are not rendered client playtesting. Client patch39 and installed binaries remain unchanged.',
            'Retains78CE/Bone/Psychic/barrier parent markers;not a fresh565-marker original-summon regression. Modern barrier death/hidedead remains deferred. No class complete,deployment or final artwork.'])
    encoded = json.dumps(result, indent=2) + '\n'
    target = ROOT / 'tools/reference/necro-town-native-evidence.json'
    if check:
        assert target.read_text() == encoded
    else:
        target.write_text(encoded)
    print('Necromancer town guards:99affected-system/30smoke markers,9wire actions per cohort,two reloads;installed artifacts and saves preserved.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
