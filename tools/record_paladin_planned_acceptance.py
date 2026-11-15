#!/usr/bin/env python3
"""Record the current Paladin cohort without certifying unresolved mechanics."""
import argparse
from collections import Counter
from pathlib import Path
import re
import sys

import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot

ROOT = e.ROOT
STAGE = ROOT / '.local/class-skills/paladin-planned-acceptance'
sys.path.insert(0, str(ROOT / 'tests'))
from check_paladin_planned_acceptance import configuration


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
    expected[config['save_result']] += 3
    assert sum(expected.values()) == 601
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
    for name in born['artifacts']:
        if name != 'Assemblies/Sanctuary.Integration.dll':
            assert born['artifacts'][name] == reuse['artifacts'][name], name
    for name in ('ModernUO.dll', 'Server.dll', 'Assemblies/UOContent.dll'):
        assert e.sha(STAGE / 'native/Distribution' / name) == born['artifacts'][name]
    build = (STAGE / 'integration-build-2.log').read_text()
    assert all(text in build for text in ('Build succeeded.', '0 Warning(s)', '0 Error(s)'))
    original = e.load(STAGE / 'failed-run-1/birth-inputs.json')
    changed = {name for name, digest in original['sourceInputs'].items()
               if name.endswith('.cs') and born['sourceInputs'][name] != digest}
    assert changed == {'tests/Sanctuary.Integration/' + name + '.cs'
                       for name in ('PaladinTravelChecks', 'HolyShieldChecks')}
    for attempt in (1, 2):
        failed = e.load(STAGE / f'failed-run-{attempt}/birth-inputs.json')
        assert failed['copiedData'] == born['copiedData']
        for name, digest in failed['artifacts'].items():
            if name != 'Assemblies/Sanctuary.Integration.dll':
                assert digest == born['artifacts'][name]
    expected = expected_markers()
    log = e.cohort(STAGE, expected, 601)
    config = configuration()
    assert tuple(len(config[key]) for key in (
        'extra_families', 'extra_checks', 'extra_target_input_commands', 'extra_files')) == (31, 15, 65, 54)
    status = e.load(ROOT / 'data/Sanctuary/Balance/class-skill-status.json')
    assert status['contentScope']['charms'] is False and status['contentScope']['runewords'] is False
    skills = [row for row in status['skills'] if row['characterClass'] == 'Paladin']
    assert len(skills) == 30 and all(row['implementation'] == 'handler-present' for row in skills)
    files = [Path(__file__).resolve(), ROOT / 'tests/check_paladin_planned_acceptance.py',
             STAGE / 'birth-inputs.json', STAGE / 'birth-native-checks.log', run,
             STAGE / 'artifact-reuse.json', STAGE / 'source-snapshot-3.json', STAGE / 'integration-build-2.log']
    files += [STAGE / f'failed-run-{n}' / name for n in (1, 2)
              for name in ('birth-inputs.json', 'birth-native-checks.log')]
    files += [ROOT / 'tools/reference' / name for name in (
        'item-content-scope-decision.json', 'paladin-binding-reference.json',
        'paladin-travel-current-evidence.json', 'paladin-holy-shield-current-evidence.json',
        'native-lich-revive-world-rule-approval.json')]
    files += [ROOT / 'docs' / name for name in (
        'paladin-planned-acceptance-checkpoint.md', 'paladin-coverage-matrix.md',
        'skill-effects.md', 'skill-icons.md')]
    result = dict(component='Current planned-content Paladin combined acceptance',
                  referenceVersion='3.3.93847', contentBuildReused=1, integrationBuild=2,
                  sourceSnapshot=3, nativeRun=3, nativeMarkers=601, markers=dict(sorted(expected.items())),
                  fixtureFamilies=31, sharedChecks=15, spellInputs=195,
                  connections=log.count('Client: 127.0.0.1: Connected.'),
                  boots=3, reloads=2, saves=3, changedSources=sorted(changed),
                  gameplayChanged=False, runtimeDataChanged=False, artifacts=born['artifacts'],
                  saveDeletionWarnings=re.findall(r'Warning: Attempted to delete (.+) during world save\.', log),
                  unsupportedWarnings=[line for line in log.splitlines() if ' WRN]' in line and 'unsupported' in line.lower()],
                  contentScope=status['contentScope'], installed=False,
                  protection=e.protection('tools/reference/slow-missiles-learned-client-evidence.json', 44),
                  goalComplete=False, classCertified=False,
                  limits='Scoped class cohort, not complete D2R parity. Exact-zero-mana Fanaticism, broader Sanctuary/Holy Freeze, unqualified recipient/item/world routes and exact-engine uncertainties remain explicit. Final effects/audio/icons and existing save-cleanup concerns remain separate.')
    e.write('paladin-planned-acceptance-native-evidence.json', result, files, check)
    print('Paladin planned cohort:601 exact results/3boots/2reloads; no class-certification claim.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    parser.add_argument('--plan', action='store_true')
    args = parser.parse_args()
    if args.plan:
        print(f'Expected exact result count: {sum(expected_markers().values())}; no receipt written.')
    else:
        record(args.check)
