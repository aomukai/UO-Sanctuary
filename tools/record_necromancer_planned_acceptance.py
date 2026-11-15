#!/usr/bin/env python3
"""Record the current class cohort without converting scoped tests into certification."""
import argparse
from collections import Counter
from pathlib import Path
import re
import sys

import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot

ROOT = e.ROOT
STAGE = ROOT / '.local/class-skills/necromancer-planned-acceptance'
sys.path.insert(0, str(ROOT / 'tests'))
from check_necromancer_planned_acceptance import planned_configuration


def expected_markers():
    previous = e.load(ROOT / 'tools/reference/necromancer-current-acceptance-native-evidence.json')
    expected = Counter(previous['fullMarkers'])
    assert sum(expected.values()) == 854
    for family in ('bone-armor-rule', 'raised-minions', 'native-corpse',
                   'native-corpse-difficulties', 'native-lich-revive'):
        expected[family + '-check'] += 1
        expected[family + '-reload-check'] += 2
    for family in ('raised-minions-live', 'native-lich-revive-live'):
        expected[family + '-check'] += 3
        expected[family + '-result'] += 3
    for family, modes in (
        ('native-corpse', ('ce-right', 'poison-quick', 'nightmare-ce-right', 'hell-poison-quick')),
        ('native-lich-revive', ('normal-right', 'normal-quick', 'nightmare-right', 'hell-quick')),
    ):
        for mode in modes:
            expected[family + '-' + mode + '-check'] += 3
            expected[family + '-' + mode + '-result'] += 3
    assert sum(expected.values()) == 929
    return expected


def record(check=False):
    # Never issue a passing receipt while the native run is incomplete.
    run = STAGE / 'native-run-6.log'
    assert 'pass across3boots/2reloads' in run.read_text(), 'Native run has not completed successfully'
    born = e.load(STAGE / 'birth-inputs.json')
    snapshot(STAGE / 'source-snapshot-9.json', STAGE / 'birth-inputs.json')
    for name, digest in born['sourceInputs'].items():
        assert e.sha(ROOT / name) == digest, name
    for name, digest in born['copiedData'].items():
        assert e.sha(ROOT / 'data' / name.removeprefix('Data/')) == digest, name
    for kind in ('Content', 'Integration'):
        assert e.sha(STAGE / kind.lower() / f'Sanctuary.{kind}.dll') == born['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
    for name in ('ModernUO.dll', 'Server.dll', 'Assemblies/UOContent.dll'):
        assert e.sha(STAGE / 'native/Distribution' / name) == born['artifacts'][name]
    for path in ('content-build-1.log', 'integration-build-9.log'):
        text = (STAGE / path).read_text()
        assert all(item in text for item in ('Build succeeded.', '0 Warning(s)', '0 Error(s)'))

    baseline = e.load(STAGE / 'failed-run-1/birth-inputs.json')
    changed = {name for name, digest in baseline['sourceInputs'].items()
               if name.endswith('.cs') and born['sourceInputs'][name] != digest}
    assert changed == {'tests/Sanctuary.Integration/' + name + '.cs' for name in (
        'BoneBarrierBirthChecks', 'BoneBarrierClockChecks', 'NativeCorpseChecks',
        'NativeCorpseDifficultyChecks', 'NativeLichReviveChecks', 'RaisedMinionChecks')}
    for attempt in range(1, 6):
        failed = e.load(STAGE / f'failed-run-{attempt}/birth-inputs.json')
        assert failed['copiedData'] == born['copiedData']
        for name, digest in failed['artifacts'].items():
            if name != 'Assemblies/Sanctuary.Integration.dll':
                assert digest == born['artifacts'][name], (attempt, name)

    expected = expected_markers()
    log = e.cohort(STAGE, expected, 929)
    save_warnings = re.findall(r'Warning: Attempted to delete (.+) during world save\.', log)
    config = planned_configuration()
    assert (len(config['extra_families']), len(config['extra_checks']),
            len(config['extra_live_commands']), len(config['extra_target_input_commands'])) == (120, 23, 35, 54)
    status = e.load(ROOT / 'data/Sanctuary/Balance/class-skill-status.json')
    assert status['contentScope']['charms'] is False and status['contentScope']['runewords'] is False
    assert sum(row['characterClass'] == 'Necromancer' and row['implementation'] == 'handler-present'
               for row in status['skills']) == 30
    protected = e.protection('tools/reference/slow-missiles-learned-client-evidence.json', 44)
    files = [Path(__file__).resolve(), ROOT / 'tests/check_necromancer_planned_acceptance.py',
             ROOT / 'src/Sanctuary.Content/WardrobeStorage.cs',
             STAGE / 'birth-inputs.json', STAGE / 'birth-native-checks.log', run,
             STAGE / 'source-snapshot-9.json', STAGE / 'content-build-1.log', STAGE / 'integration-build-9.log']
    files += [STAGE / f'failed-run-{n}' / name for n in range(1, 6)
              for name in ('birth-inputs.json', 'birth-native-checks.log')]
    files += [ROOT / 'tools/reference' / name for name in (
        'item-content-scope-decision.json', 'necromancer-current-acceptance-native-evidence.json',
        'necro-prison-expiry-acceptance-evidence.json', 'necro-barrier-reload-acceptance-evidence.json',
        'necro-native-corpse-retention-evidence.json', 'raised-helper-observer-native-evidence.json')]
    files += [ROOT / 'docs' / name for name in (
        'necromancer-planned-acceptance-checkpoint.md', 'necromancer-coverage-matrix.md',
        'skill-effects.md', 'skill-icons.md')]
    result = dict(component='Current planned-content Necromancer combined acceptance',
                  referenceVersion='3.3.93847', contentBuild=1, integrationBuild=9,
                  sourceSnapshot=9, nativeRun=6, nativeMarkers=929, markers=dict(sorted(expected.items())),
                  fixtureFamilies=120, sharedChecks=23, liveActions=111, inputActions=162,
                  connections=log.count('Client: 127.0.0.1: Connected.'), boots=3, reloads=2, saves=3,
                  saveDeletionWarnings=save_warnings,
                  saveWarningScope='Existing WardrobeEquipment WorldSave pruning deletes sets for absent owners during save. The warnings remain a separate save-cleanup concern; this receipt does not claim a warning-free native run.',
                  gameplayChangedDuringAcceptance=False, runtimeDataChangedDuringAcceptance=False,
                  changedSources=sorted(changed), artifacts=born['artifacts'], contentScope=status['contentScope'],
                  installed=False, protection=protected, goalComplete=False, classCertified=False,
                  limits='Scoped class cohort only. Dedicated newer receipts, unsupported mechanics and exact-engine uncertainties remain attributed separately. Final effects/audio and icons are tracked separately.')
    e.write('necromancer-planned-acceptance-native-evidence.json', result, files, check)
    print('Necromancer planned cohort:929 exact markers/3boots/2reloads; no class-certification claim.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    parser.add_argument('--plan', action='store_true')
    args = parser.parse_args()
    if args.plan:
        print(f'Expected exact combined marker count: {sum(expected_markers().values())}; no receipt written.')
    else:
        record(args.check)
