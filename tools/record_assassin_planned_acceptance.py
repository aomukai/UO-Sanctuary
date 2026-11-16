#!/usr/bin/env python3
"""Record current scoped Assassin acceptance without certifying full parity."""
import argparse
from collections import Counter
import re
import sys

import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot

ROOT = e.ROOT
STAGE = ROOT / '.local/class-skills/assassin-planned-acceptance'
sys.path.insert(0, str(ROOT / 'tests'))
from check_assassin_planned_acceptance import configuration, LEGACY


def expected_markers():
    config = configuration()
    expected = Counter()

    def add(command, count):
        expected[command] += count
        results = config['extra_wait_results'].get(command, ())
        for name in ((results,) if isinstance(results, str) else results):
            expected[name] += count

    add('iron-golem-birth-check', 3)
    for family in ('iron-golem-items', 'npc-resource-callbacks', *config['extra_families']):
        add(family + '-check', 1)
        add(family + '-reload-check', 2)
    for command in config['extra_checks']:
        add(command, 3)
    for command in config['extra_target_input_commands']:
        expected[command + '-check'] += 3
        expected[command + '-result'] += 3
    for name in LEGACY:
        expected[name + '-input-check'] += 3
        expected[name + '-input-result'] += 3
    for source in (0, 1, 9):
        expected[f'fury-held-wire-{source}-result'] += 3
    expected['fury-held-input-save-stage-check'] += 3
    assert sum(expected.values()) == 655
    return expected


def record(check=False):
    run = STAGE / 'native-run-3.log'
    assert 'pass across3boots/2reloads' in run.read_text(), 'Native run has not completed successfully'
    born = e.load(STAGE / 'birth-inputs.json')
    snapshot(STAGE / 'source-snapshot-3.json', STAGE / 'birth-inputs.json')
    for name, digest in born['sourceInputs'].items():
        assert e.sha(ROOT / name) == digest, name
    for name, digest in born['copiedData'].items():
        assert e.sha(ROOT / 'data' / name.removeprefix('Data/')) == digest, name
    for kind in ('Content', 'Integration'):
        assert e.sha(STAGE / kind.lower() / f'Sanctuary.{kind}.dll') == born['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
    reuse = e.load(STAGE / 'artifact-reuse.json')
    assert e.sha(ROOT / reuse['parentCheckpoint']) == reuse['parentCheckpointSha256']
    for name, digest in born['artifacts'].items():
        if name != 'Assemblies/Sanctuary.Integration.dll':
            assert digest == reuse['artifacts'][name], name
    assert e.sha(STAGE / 'prechange-integration/Sanctuary.Integration.dll') == reuse['artifacts']['Assemblies/Sanctuary.Integration.dll']
    for name in ('ModernUO.dll', 'Server.dll', 'Assemblies/UOContent.dll'):
        assert e.sha(STAGE / 'native/Distribution' / name) == born['artifacts'][name]
    build = (STAGE / 'integration-build-2.log').read_text()
    assert all(text in build for text in ('Build succeeded.', '0 Warning(s)', '0 Error(s)'))
    baseline = e.load(STAGE / 'source-snapshot-1.json')['inputs']['sourceInputs']
    changed = {name for name, digest in baseline.items() if born['sourceInputs'][name] != digest}
    assert changed == {'tests/Sanctuary.Integration/AssassinCastClockChecks.cs',
                       'tests/Sanctuary.Integration/WakeLearnedChecks.cs'}
    original = e.load(STAGE / 'failed-run-1/birth-inputs.json')
    assert original['copiedData'] == born['copiedData'] == reuse['copiedData']
    for name, count, same in (
        ('assassin-clock-current-native-evidence.json', 30, False),
        ('assassin-wake-placement-current-native-evidence.json', 15, True),
    ):
        focused = e.load(ROOT / 'tools/reference' / name)
        assert focused['nativeMarkers'] == count
        for artifact, digest in focused['artifacts'].items():
            if same or artifact != 'Assemblies/Sanctuary.Integration.dll':
                assert digest == born['artifacts'][artifact], artifact
        for path, digest in focused['files'].items():
            assert e.sha(ROOT / path) == digest, path
    log = e.cohort(STAGE, expected_markers(), 655)
    config = configuration()
    assert tuple(len(config[key]) for key in (
        'extra_families', 'extra_checks', 'extra_target_input_commands', 'extra_files')) == (66, 26, 34, 98)
    connections = sum(expected_markers()[name] for name in config['extra_connected_checks'])
    connections += 3 * (len(config['extra_target_input_commands']) + len(LEGACY) + 4)
    assert log.count('Client: 127.0.0.1: Connected.') == connections
    assert 'denied by connection filter' not in log
    status = e.load(ROOT / 'data/Sanctuary/Balance/class-skill-status.json')
    assert status['contentScope']['charms'] is False and status['contentScope']['runewords'] is False
    skills = [row for row in status['skills'] if row['characterClass'] == 'Assassin']
    assert len(skills) == 30 and all(row['implementation'] == 'handler-present' for row in skills)
    files = [ROOT / 'tools/record_assassin_planned_acceptance.py',
             ROOT / 'tests/check_assassin_planned_acceptance.py',
             STAGE / 'birth-inputs.json', STAGE / 'birth-native-checks.log', run,
             STAGE / 'artifact-reuse.json', STAGE / 'source-snapshot-1.json',
             STAGE / 'source-snapshot-3.json', STAGE / 'integration-build-2.log',
             STAGE / 'failed-run-1/birth-inputs.json', STAGE / 'failed-run-1/birth-native-checks.log',
             STAGE / 'failed-run-1/sources/tests/check_assassin_planned_acceptance.py']
    files += [STAGE / 'failed-run-2' / name for name in ('birth-inputs.json', 'birth-native-checks.log', 'sources/tests/check_assassin_planned_acceptance.py')]
    files += [ROOT / 'tools/reference' / name for name in (
        'item-content-scope-decision.json', 'assassin-clock-current-native-evidence.json',
        'assassin-wake-placement-current-native-evidence.json',
        'wake-learned-sanctuary-decision.json', 'shadow-learned-cast-native-evidence.json',
        'sentry-placement-native-evidence.json')]
    files += [ROOT / 'docs' / name for name in (
        'assassin-planned-acceptance-checkpoint.md', 'assassin-coverage-matrix.md',
        'dragon-talon-approved-action-rule.md', 'dragon-claw-approved-action-rule.md',
        'dragon-flight-approved-action-rule.md', 'blade-fury-approved-input-rule.md',
        'sentry-occupied-placement-proposal.md', 'skill-effects.md', 'skill-icons.md')]
    result = dict(component='Current planned-content Assassin combined acceptance',
                  referenceVersion='3.3.93847', contentBuildReused=1, integrationBuild=2,
                  sourceSnapshot=3, nativeRun=3, nativeMarkers=655,
                  markers=dict(sorted(expected_markers().items())), fixtureFamilies=66,
                  sharedChecks=26, spellInputAttempts=102, legacyWireSequences=42,
                  heldFuryWireSequences=9, connectedFurySaveStages=3,
                  connections=connections, boots=3, reloads=2, saves=3,
                  changedSources=sorted(changed), gameplayChanged=False,
                  runtimeDataChanged=False, artifacts=born['artifacts'],
                  saveDeletionWarnings=re.findall(r'Warning: Attempted to delete (.+) during world save\.', log),
                  unsupportedWarnings=[line for line in log.splitlines() if ' WRN]' in line and 'unsupported' in line.lower()],
                  contentScope=status['contentScope'], installed=False,
                  protection=e.protection('tools/reference/slow-missiles-learned-client-evidence.json', 44),
                  goalComplete=False, classCertified=False,
                  limits='Scoped class cohort, not full D2R parity. Current executable timing/RNG/stat/event/geometry/owner/PvP equivalence, unsupported original-item callbacks, exceptional terrain/target/recipient cases and graphical-client acceptance remain explicit. Component actors and prepared states retain bounded claims; Shadow learned/AI controls supplement dedicated intrinsic proofs. Final effects/audio/icons and historical save-cleanup concerns remain separate.')
    e.write('assassin-planned-acceptance-native-evidence.json', result, files, check)
    print('Assassin planned cohort:655 exact results/3boots/2reloads; no class-certification claim.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    parser.add_argument('--plan', action='store_true')
    args = parser.parse_args()
    if args.plan:
        print(f'Expected exact result count: {sum(expected_markers().values())}; no receipt written.')
    else:
        record(args.check)
