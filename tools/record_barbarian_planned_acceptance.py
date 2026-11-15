#!/usr/bin/env python3
"""Record current scoped Barbarian acceptance without certifying full parity."""
import argparse
from collections import Counter
import re
import sys

import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot

ROOT = e.ROOT
STAGE = ROOT / '.local/class-skills/barbarian-planned-acceptance'
sys.path.insert(0, str(ROOT / 'tests'))
from check_barbarian_planned_acceptance import configuration


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
    for command in config['extra_live_commands']:
        expected[command + '-check'] += 3
        assert command in config['checked_live_commands']
        expected[command + '-result'] += 3
    # The retained wire callback executes these once on each boot.
    for command in ('find-item-snapshot-check', 'find-potion-snapshot-check'):
        expected[command] += 3
    assert sum(expected.values()) == 245
    return expected


def record(check=False):
    run = STAGE / 'native-run-2.log'
    assert 'pass across3boots/2reloads' in run.read_text(), 'Native run has not completed successfully'
    born = e.load(STAGE / 'birth-inputs.json')
    snapshot(STAGE / 'source-snapshot-4.json', STAGE / 'birth-inputs.json')
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
    for name in ('ModernUO.dll', 'Server.dll', 'Assemblies/UOContent.dll'):
        assert e.sha(STAGE / 'native/Distribution' / name) == born['artifacts'][name]
    build = (STAGE / 'integration-build-3.log').read_text()
    assert all(text in build for text in ('Build succeeded.', '0 Warning(s)', '0 Error(s)'))
    original = e.load(STAGE / 'failed-run-1/birth-inputs.json')
    changed = {name for name, digest in original['sourceInputs'].items()
               if name.endswith('.cs') and born['sourceInputs'][name] != digest}
    assert changed == {'tests/Sanctuary.Integration/WarCryChecks.cs'}
    assert original['copiedData'] == born['copiedData']
    focused = e.load(ROOT / 'tools/reference/barbarian-warcry-current-native-evidence.json')
    assert focused['artifacts'] == born['artifacts'] and focused['nativeMarkers'] == 15
    for name, digest in focused['files'].items():
        assert e.sha(ROOT / name) == digest, name
    log = e.cohort(STAGE, expected_markers(), 245)
    config = configuration()
    assert tuple(len(config[key]) for key in (
        'extra_families', 'extra_checks', 'extra_target_input_commands', 'extra_files')) == (25, 9, 13, 20)
    status = e.load(ROOT / 'data/Sanctuary/Balance/class-skill-status.json')
    assert status['contentScope']['charms'] is False and status['contentScope']['runewords'] is False
    skills = [row for row in status['skills'] if row['characterClass'] == 'Barbarian']
    assert len(skills) == 30 and all(row['implementation'] == 'handler-present' for row in skills)
    files = [ROOT / 'tools/record_barbarian_planned_acceptance.py',
             ROOT / 'tests/check_barbarian_planned_acceptance.py',
             STAGE / 'birth-inputs.json', STAGE / 'birth-native-checks.log', run,
             STAGE / 'artifact-reuse.json', STAGE / 'source-snapshot-4.json',
             STAGE / 'integration-build-3.log', STAGE / 'failed-run-1/birth-inputs.json',
             STAGE / 'failed-run-1/birth-native-checks.log']
    files += [ROOT / 'tools/reference' / name for name in (
        'item-content-scope-decision.json', 'barbarian-warcry-current-native-evidence.json',
        'double-swing-start-decision.json', 'frenzy-start-decision.json',
        'grim-ward-context-decision.json', 'leap-attack-landing-decision.json')]
    files += [ROOT / 'docs' / name for name in (
        'barbarian-planned-acceptance-checkpoint.md', 'barbarian-coverage-matrix.md',
        'skill-effects.md', 'skill-icons.md')]
    result = dict(component='Current planned-content Barbarian combined acceptance',
                  referenceVersion='3.3.93847', contentBuildReused=1, integrationBuild=3,
                  sourceSnapshot=4, nativeRun=2, nativeMarkers=245,
                  markers=dict(sorted(expected_markers().items())), fixtureFamilies=25,
                  sharedChecks=9, spellInputs=39, checkedLiveActions=3,
                  snapshotCallbacks=6, connections=log.count('Client: 127.0.0.1: Connected.'),
                  boots=3, reloads=2, saves=3, changedSources=sorted(changed),
                  gameplayChanged=False, runtimeDataChanged=False, artifacts=born['artifacts'],
                  saveDeletionWarnings=re.findall(r'Warning: Attempted to delete (.+) during world save\.', log),
                  unsupportedWarnings=[line for line in log.splitlines() if ' WRN]' in line and 'unsupported' in line.lower()],
                  contentScope=status['contentScope'], installed=False,
                  protection=e.protection('tools/reference/slow-missiles-learned-client-evidence.json', 44),
                  goalComplete=False, classCertified=False,
                  limits='Scoped class cohort, not full D2R parity. Bash post-allocation total, Find Item chance/special treasure, native Unique boss stun mapping, exceptional hand/landing/recipient cases and exact modern timing/event/RNG/world equivalence remain explicit. Broader melee conveniences, graphical-client acceptance, final effects/audio/icons and existing save-cleanup concerns remain separate.')
    e.write('barbarian-planned-acceptance-native-evidence.json', result, files, check)
    print('Barbarian planned cohort:245 exact results/3boots/2reloads; no class-certification claim.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    parser.add_argument('--plan', action='store_true')
    args = parser.parse_args()
    if args.plan:
        print(f'Expected exact result count: {sum(expected_markers().values())}; no receipt written.')
    else:
        record(args.check)
