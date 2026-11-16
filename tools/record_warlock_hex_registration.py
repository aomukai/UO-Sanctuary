#!/usr/bin/env python3
"""Bind learned Hex registration and single-contact Purge fixture isolation."""
import argparse
from collections import Counter

import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot

ROOT = e.ROOT
BASE = ROOT / '.local/class-skills/warlock-weapons-acceptance'
STAGE = ROOT / '.local/class-skills/warlock-hex-registration-current-4'


def record(check=False):
    run = BASE / 'hex-focused-run-4.log'
    assert 'pass across3boots/2reloads' in run.read_text()
    born = e.load(STAGE / 'birth-inputs.json')
    snapshot(BASE / 'source-snapshot-5.json', STAGE / 'birth-inputs.json')
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
    before = e.load(BASE / 'source-snapshot-1.json')['inputs']['sourceInputs']
    changed = {name for name, digest in before.items() if digest != born['sourceInputs'][name]}
    assert changed == {'tests/Sanctuary.Integration/' + name + '.cs'
        for name in ('WeaponHexGroupChecks', 'HexPurgeCastChecks', 'HexBaneCastChecks')}
    build = (BASE / 'integration-build-4.log').read_text()
    assert all(value in build for value in ('Build succeeded.', '0 Warning(s)', '0 Error(s)'))
    expected = Counter({'iron-golem-birth-check': 3,
        'purge-cast-check-result': 1, 'purge-cast-reload-check-result': 2})
    for family in ('iron-golem-items', 'npc-resource-callbacks', 'bane-cast',
        'siphon-cast', 'hex-group', 'purge-cast', 'purge-reaction', 'weapon-landed'):
        expected[family + '-check'] = 1
        expected[family + '-reload-check'] = 2
    for family in ('bane-cast', 'siphon-cast', 'purge-cast'):
        for mode in ('right', 'quick'):
            expected[f'{family}-{mode}-check'] = 3
            expected[f'{family}-{mode}-result'] = 3
    log = e.cohort(STAGE, expected, 66)
    assert log.count('Client: 127.0.0.1: Connected.') == 21
    assert 'denied by connection filter' not in log
    files = [ROOT / 'tools/record_warlock_hex_registration.py',
        ROOT / 'tests/check_warlock_weapons_acceptance.py',
        ROOT / 'tests/check_iron_golem_birth.py',
        BASE / 'source-snapshot-1.json', BASE / 'source-snapshot-5.json',
        BASE / 'integration-build-4.log', BASE / 'artifact-reuse.json',
        BASE / 'hex-focused-driver-4.py', run,
        STAGE / 'birth-inputs.json', STAGE / 'birth-native-checks.log',
        BASE / 'failed-run-1/birth-inputs.json', BASE / 'failed-run-1/birth-native-checks.log']
    files += [ROOT / name for name in changed]
    for number in (1, 2, 3):
        failed = BASE / f'failed-hex-focus-{number}'
        files += [failed / name for name in ('birth-inputs.json', 'birth-native-checks.log',
            'sources/tests/Sanctuary.Integration/HexPurgeCastChecks.cs')]
    result = dict(component='Three learned Hex routes and isolated Purge single contact',
        referenceVersion='3.3.93847', contentBuildReused=1, integrationBuild=4,
        sourceSnapshot=5, nativeRun=4, nativeMarkers=66, markers=dict(expected),
        fixtureFamilies=6, inputAttempts=18, connections=21,
        boots=3, saves=3, reloads=2, changedSources=sorted(changed),
        artifacts=born['artifacts'], gameplayChanged=False, runtimeDataChanged=False,
        installed=False, classCertified=False,
        protection=e.protection('tools/reference/slow-missiles-learned-client-evidence.json', 44),
        limits='Fixture-only corrections: require approved Purge learned registration and relocate the original saved Purge actors into a corridor clear of foreign retained actors. All group replacement, independent owner/recipient state, failed-call preservation, exact item/resource/snapshot, one-contact/one-seed, reaction and actual input assertions remain. An earlier Bane saved-state failure did not recur after field-specific diagnostics; its exclusive cause remains unassigned. Whole weapon group acceptance remains pending.')
    e.write('warlock-hex-registration-native-evidence.json', result, files, check)
    print('Warlock Hex focus:66 exact results/18inputs/3saves/2reloads; gameplay unchanged.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
