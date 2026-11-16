#!/usr/bin/env python3
"""Bind Arctic's isolated synthetic clock to real shared-world save controls."""
import argparse
from collections import Counter

import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot

ROOT = e.ROOT
BASE = ROOT / '.local/class-skills/druid-planned-acceptance'
STAGE = ROOT / '.local/class-skills/druid-arctic-composition-focus-5'


def record(check=False):
    run = BASE / 'arctic-focused-run-5.log'
    assert 'pass across3boots/2reloads' in run.read_text()
    born = e.load(STAGE / 'birth-inputs.json')
    snapshot(BASE / 'source-snapshot-14.json', STAGE / 'birth-inputs.json')
    for name, digest in born['sourceInputs'].items():
        assert e.sha(ROOT / name) == digest, name
    reuse = e.load(BASE / 'artifact-reuse.json')
    assert e.sha(ROOT / reuse['parentCheckpoint']) == reuse['parentCheckpointSha256']
    assert born['copiedData'] == reuse['copiedData']
    for name, digest in born['artifacts'].items():
        if name != 'Assemblies/Sanctuary.Integration.dll':
            assert digest == reuse['artifacts'][name], name
    for kind in ('Content', 'Integration'):
        assert e.sha(STAGE / kind.lower() / f'Sanctuary.{kind}.dll') == born['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
    before = e.load(BASE / 'failed-run-3/birth-inputs.json')['sourceInputs']
    changed = {name for name, digest in before.items()
               if name.endswith('.cs') and digest != born['sourceInputs'][name]}
    assert changed == {'tests/Sanctuary.Integration/' + name + '.cs'
                      for name in ('ArcticStreamChecks', 'ArcticHeldInputChecks')}
    build = (BASE / 'integration-build-12.log').read_text()
    assert all(value in build for value in ('Build succeeded.', '0 Warning(s)', '0 Error(s)'))
    expected = Counter({'iron-golem-birth-check': 3})
    for family in ('iron-golem-items', 'npc-resource-callbacks', 'arctic-potion',
                   'arctic-stream', 'arctic-held-input'):
        expected[family + '-check'] = 1
        expected[family + '-reload-check'] = 2
    for source in (0, 1, 9):
        expected[f'arctic-held-wire-{source}-result'] = 3
    for name in ('arctic-held-input-save-stage-check', 'arctic-stream-live-check',
                 'arctic-stream-live-result', 'arctic-stream-expiry-stage-check'):
        expected[name] = 3
    log = e.cohort(STAGE, expected, 39)
    assert log.count('Arctic save target ') == 3
    assert log.count('Client: 127.0.0.1: Connected.') == 18
    assert 'denied by connection filter' not in log
    files = [ROOT / 'tools/record_druid_arctic_composition.py',
        ROOT / 'tests/check_druid_planned_acceptance.py', ROOT / 'tests/check_iron_golem_birth.py',
        BASE / 'arctic-focus-driver-5.py', BASE / 'arctic-focused-run-5.log',
        BASE / 'source-snapshot-14.json', BASE / 'integration-build-12.log',
        BASE / 'artifact-reuse.json', STAGE / 'birth-inputs.json', STAGE / 'birth-native-checks.log']
    files += [ROOT / name for name in changed]
    for number in range(1, 5):
        failed = BASE / f'failed-arctic-focus-{number}'
        files += [failed / name for name in ('birth-inputs.json', 'birth-native-checks.log')]
        files += [failed / 'sources/tests/Sanctuary.Integration' / (name + '.cs')
                  for name in ('ArcticStreamChecks', 'ArcticHeldInputChecks')]
    result = dict(component='Arctic synthetic-clock isolation and retained target wounds',
        referenceVersion='3.3.93847', contentBuildReused=1, integrationBuild=12,
        sourceSnapshot=14, nativeRun=5, nativeMarkers=39, markers=dict(expected),
        heldWireSequences=9, naturalStreamChecks=3, connections=18,
        boots=3, saves=3, reloads=2, changedSources=sorted(changed),
        gameplayChanged=False, runtimeDataChanged=False, installed=False,
        artifacts=born['artifacts'], classCertified=False,
        protection=e.protection('tools/reference/slow-missiles-learned-client-evidence.json', 44),
        limits='Fixture-only clock isolation: original cold/potion/held packets, RNG, damage, resources, wounds, overdue expiry and no input replay retained. Empty temporary missile set prevents a synthetic future tick from damaging other fixtures; the entire surrounding pool is restored and compared. Combined Druid acceptance remains pending; no current-executable parity claim.')
    e.write('druid-arctic-composition-native-evidence.json', result, files, check)
    print('Arctic composition:39 exact results/3saves/2reloads; gameplay unchanged.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
