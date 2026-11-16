#!/usr/bin/env python3
"""Bind the shared Cleave/Mirrored fixture location change to original controls."""
import argparse
from collections import Counter

import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot

ROOT = e.ROOT
BASE = ROOT / '.local/class-skills/warlock-weapons-acceptance'
STAGE = ROOT / '.local/class-skills/warlock-cleave-fixture-space-current'


def record(check=False):
    run = BASE / 'fixture-space-focused-run-1.log'
    assert 'pass across3boots/2reloads' in run.read_text()
    born = e.load(STAGE / 'birth-inputs.json')
    snapshot(BASE / 'source-snapshot-6.json', STAGE / 'birth-inputs.json')
    for name, digest in born['sourceInputs'].items():
        assert e.sha(ROOT / name) == digest, name
    before = e.load(BASE / 'failed-run-2/birth-inputs.json')
    changed = {name for name, digest in before['sourceInputs'].items()
        if name.endswith('.cs') and digest != born['sourceInputs'][name]}
    source = 'tests/Sanctuary.Integration/CleaveActionChecks.cs'
    assert changed == {source} and born['copiedData'] == before['copiedData']
    text = (BASE / 'failed-run-2/sources' / source).read_text()
    assert (ROOT / source).read_text() == text.replace(
        'FixtureSpace.Corridor(p,6560,20,3500)', 'FixtureSpace.Corridor(p,6560,20,1000)', 1)
    for name, digest in born['artifacts'].items():
        if name != 'Assemblies/Sanctuary.Integration.dll':
            assert digest == before['artifacts'][name], name
    for kind in ('Content', 'Integration'):
        assert e.sha(STAGE / kind.lower() / f'Sanctuary.{kind}.dll') == born['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
    build = (BASE / 'integration-build-5.log').read_text()
    assert all(value in build for value in ('Build succeeded.', '0 Warning(s)', '0 Error(s)'))
    expected = Counter({'iron-golem-birth-check': 3})
    for family in ('iron-golem-items', 'npc-resource-callbacks',
                   'cleave-action', 'magic-arrow-cast', 'mirrored-packets'):
        expected[family + '-check'] = 1
        expected[family + '-reload-check'] = 2
    e.cohort(STAGE, expected, 18)
    files = [ROOT / 'tools/record_warlock_fixture_space.py', ROOT / source,
        ROOT / 'tests/check_warlock_weapons_acceptance.py',
        BASE / 'failed-run-2/sources' / source,
        BASE / 'failed-run-2/birth-inputs.json', BASE / 'failed-run-2/birth-native-checks.log',
        BASE / 'source-snapshot-6.json', BASE / 'integration-build-5.log', run,
        STAGE / 'birth-inputs.json', STAGE / 'birth-native-checks.log']
    result = dict(component='Shared Cleave/Mirrored fixture has enough empty test rows',
        referenceVersion='3.3.93847', contentBuildReused=1, integrationBuild=5,
        sourceSnapshot=6, nativeRun=1, nativeMarkers=18, markers=dict(expected),
        boots=3, saves=3, reloads=2, changedSources=[source],
        artifacts=born['artifacts'], gameplayChanged=False, runtimeDataChanged=False,
        installed=False, classCertified=False,
        protection=e.protection('tools/reference/slow-missiles-learned-client-evidence.json', 44),
        limits='Only the shared fixture starting row changes, from3500 to1000. Existing terrain/fit/LOS, skill range/contact, damage, original actor/item and save assertions remain. Original Cleave, Magic Arrow and Mirrored packet controls pass; full nine-route group remains separate. Earlier Bane intermittent failure is not explained by this change.')
    e.write('warlock-fixture-space-native-evidence.json', result, files, check)
    print('Warlock shared fixture space:18 exact results/3saves/2reloads; gameplay unchanged.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
