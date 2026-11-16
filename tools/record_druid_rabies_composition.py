#!/usr/bin/env python3
"""Bind the shared-world Rabies fixture correction to its actual reload checks."""
import argparse
from collections import Counter

import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot

ROOT = e.ROOT
BASE = ROOT / '.local/class-skills/druid-planned-acceptance'
STAGE = ROOT / '.local/class-skills/druid-rabies-migration-current-7'


def record(check=False):
    run = BASE / 'rabies-migration-focused-run-7.log'
    assert 'pass across3boots/2reloads' in run.read_text()
    born = e.load(STAGE / 'birth-inputs.json')
    snapshot(BASE / 'source-snapshot-10.json', STAGE / 'birth-inputs.json')
    for name, digest in born['sourceInputs'].items():
        assert e.sha(ROOT / name) == digest, name
    for kind in ('Content', 'Integration'):
        assert e.sha(STAGE / kind.lower() / f'Sanctuary.{kind}.dll') == born['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
    reuse = e.load(BASE / 'artifact-reuse.json')
    assert e.sha(ROOT / reuse['parentCheckpoint']) == reuse['parentCheckpointSha256']
    assert born['copiedData'] == reuse['copiedData']
    for name, digest in born['artifacts'].items():
        if name != 'Assemblies/Sanctuary.Integration.dll':
            assert digest == reuse['artifacts'][name], name
    before = e.load(BASE / 'source-snapshot-1.json')['inputs']['sourceInputs']
    changed = {name for name, digest in before.items() if digest != born['sourceInputs'][name]}
    assert changed == {'tests/Sanctuary.Integration/' + name + '.cs' for name in (
        'DruidHumanCastClockChecks', 'DruidRabiesChecks',
        'DruidRabiesInfectionChecks', 'DruidRabiesLifecycleChecks')}
    build = (BASE / 'integration-build-8.log').read_text()
    assert all(value in build for value in ('Build succeeded.', '0 Warning(s)', '0 Error(s)'))
    expected = Counter({'iron-golem-birth-check': 3})
    for name in ('iron-golem-items', 'npc-resource-callbacks', 'druid-rabies',
                 'druid-rabies-infection', 'druid-rabies-lifecycle'):
        expected[name + '-check'] = 1
        expected[name + '-reload-check'] = 2
    for name in ('druid-rabies', 'druid-rabies-infection', 'druid-rabies-lifecycle'):
        expected[name + '-snapshot-check'] = 3
    log = e.cohort(STAGE, expected, 27)
    files = [ROOT / 'tools/record_druid_rabies_composition.py',
             ROOT / 'tests/check_druid_planned_acceptance.py',
             ROOT / 'tests/check_iron_golem_birth.py',
             STAGE / 'birth-inputs.json', STAGE / 'birth-native-checks.log', run,
             BASE / 'artifact-reuse.json', BASE / 'source-snapshot-1.json',
             BASE / 'source-snapshot-10.json', BASE / 'integration-build-8.log']
    files += [ROOT / name for name in changed]
    for number in range(1, 7):
        failed = BASE / f'failed-rabies-focus-{number}'
        files += [failed / name for name in ('birth-inputs.json', 'birth-native-checks.log')]
        files += list((failed / 'sources/tests/Sanctuary.Integration').glob('DruidRabies*Checks.cs'))
    result = dict(component='Rabies fixtures coexist across actual world saves/reloads',
                  referenceVersion='3.3.93847', integrationBuild=8, sourceSnapshot=10,
                  nativeRun=7, nativeMarkers=27, markers=dict(expected),
                  boots=3, saves=3, reloads=2, changedSources=sorted(changed),
                  gameplayChanged=False, runtimeDataChanged=False,
                  artifacts=born['artifacts'], classCertified=False, installed=False,
                  saveDeletionWarnings=[line for line in log.splitlines() if 'Attempted to delete' in line],
                  protection=e.protection('tools/reference/slow-missiles-learned-client-evidence.json', 44),
                  limits='Fixture-only correction: controlled version-0 payload, owned state comparisons, global no-replay and unrelated-state preservation. Loaded offline children may age/reschedule and record the existing facet/logout fault without moving, damage or RNG. Combined Druid acceptance remains pending; no modern parity certification.')
    e.write('druid-rabies-composition-native-evidence.json', result, files, check)
    print('Rabies composition:27 exact results/3saves/2reloads; gameplay unchanged.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
