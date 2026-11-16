#!/usr/bin/env python3
"""Record the disabled prepared explosive terrain mapping and its regressions."""
import argparse
from collections import Counter
import re
import sys

import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot

ROOT = e.ROOT
STAGE = ROOT / '.local/class-skills/explosive-terrain'
sys.path.insert(0, str(ROOT / 'tests'))
from check_explosive_terrain import configuration


def expected_markers():
    config = configuration()
    expected = Counter({'iron-golem-birth-check': 3})
    for family in ('iron-golem-items', 'npc-resource-callbacks', *config['extra_families']):
        for suffix, count in (('-check', 1), ('-reload-check', 2)):
            command = family + suffix
            expected[command] += count
            if command in config['extra_wait_results']:
                expected[config['extra_wait_results'][command]] += count
    for command in config['extra_target_input_commands']:
        expected[command + '-check'] += 3
        expected[command + '-result'] += 3
    assert sum(expected.values()) == 46
    return expected


def record(check=False):
    run = STAGE / 'native-run-4.log'
    assert 'pass across3boots/2reloads' in run.read_text()
    born = e.load(STAGE / 'birth-inputs.json')
    snapshot(STAGE / 'source-snapshot-4.json', STAGE / 'birth-inputs.json')
    for name, digest in born['sourceInputs'].items():
        assert e.sha(ROOT / name) == digest, name
    parent = e.load(ROOT / '.local/class-skills/warlock-chaos-acceptance/birth-inputs.json')
    assert born['copiedData'] == parent['copiedData']
    names = {name for name in born['sourceInputs'] if name.endswith('.cs')}
    changed = {name for name in names if born['sourceInputs'][name] != parent['sourceInputs'].get(name)}
    assert changed == {'src/Sanctuary.Content/SorceressSpellMissiles.cs',
                       'tests/Sanctuary.Integration/ExplosiveTerrainChecks.cs'}
    initial = e.load(STAGE / 'source-snapshot-1.json')['inputs']['sourceInputs']
    for name in names:
        if not name.startswith('tests/'):
            assert born['sourceInputs'][name] == initial[name], name
    for name, digest in born['copiedData'].items():
        assert e.sha(ROOT / 'data' / name.removeprefix('Data/')) == digest, name
    for name in ('ModernUO.dll', 'Server.dll', 'Assemblies/UOContent.dll'):
        assert e.sha(STAGE / 'native/Distribution' / name) == born['artifacts'][name] == parent['artifacts'][name]
    for kind in ('Content', 'Integration'):
        assert e.sha(STAGE / kind.lower() / f'Sanctuary.{kind}.dll') == born['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
    for build in ('content-build-1.log', 'integration-build-4.log'):
        text = (STAGE / build).read_text()
        assert all(value in text for value in ('Build succeeded.', '0 Warning(s)', '0 Error(s)'))
    source = (ROOT / 'src/Sanctuary.Content/SorceressSpellMissiles.cs').read_text()
    assert 'internal static bool PreparedExplosiveTerrain;' in source
    assert 'PreparedExplosiveTerrain&&flight.Npc==null' in source
    assert 'LobProjectiles.Advance(map,flight.X,flight.Y,flight.Z,next.Vx,next.Vy,int.MaxValue)' in source
    log = e.cohort(STAGE, expected_markers(), 46)
    assert log.count('Client: 127.0.0.1: Connected.') == 16
    assert 'denied by connection filter' not in log
    files = [ROOT / 'tools/record_explosive_terrain.py',
        ROOT / 'docs/explosive-terrain-checkpoint.md', ROOT / 'docs/explosive-terrain-rule-proposal.md',
        ROOT / 'tools/reference/explosive-terrain-rule-proposal.json',
        ROOT / 'src/Sanctuary.Content/SorceressSpellMissiles.cs',
        ROOT / 'tests/Sanctuary.Integration/ExplosiveTerrainChecks.cs',
        ROOT / 'src/Sanctuary.Content/LobProjectiles.cs',
        STAGE / 'source-snapshot-1.json', STAGE / 'source-snapshot-4.json',
        STAGE / 'content-build-1.log', STAGE / 'integration-build-4.log',
        STAGE / 'birth-inputs.json', STAGE / 'birth-native-checks.log', run]
    files += list(configuration()['extra_inputs'])
    for number in (1, 2, 3):
        files += [STAGE / f'failed-run-{number}/birth-inputs.json',
                  STAGE / f'failed-run-{number}/birth-native-checks.log']
    result = dict(component='Prepared Sorceress Fire Ball/Glacial shared native terrain rule',
        referenceVersion='3.3.93847', approvalRequired=True, defaultRuleEnabled=False,
        nativeMarkers=46, markers=dict(sorted(expected_markers().items())),
        defaultWireInputs=12, preparedDirectionalWalls=16, preparedOpenCases=4,
        preparedResumedCopies=4, connections=16, boots=3, saves=3, reloads=2,
        contentBuild=1, integrationBuild=4, sourceSnapshot=4, nativeRun=4,
        artifacts=born['artifacts'], changedSources=sorted(changed),
        runtimeDataChanged=False, installed=False, classCertified=False, goalComplete=False,
        saveDeletionWarnings=re.findall(r'Warning: Attempted to delete (.+) during world save\.', log),
        unsupportedWarnings=[line for line in log.splitlines() if ' WRN]' in line and 'unsupported' in line.lower()],
        protection=e.protection('tools/reference/slow-missiles-learned-client-evidence.json', 44),
        limits='Prepared test-only rule; default live paths unchanged. Exact current D2R tile mapping remains unknown. Learned-input qualification under adopted rule follows a decision. NPC/mage and other explosive controllers unchanged; two WardrobeSet save warnings remain recorded. No charms/runewords or final assets.')
    e.write('explosive-terrain-prepared-native-evidence.json', result, files, check)
    print('Prepared explosive terrain:46 results/12 default wire inputs/2reloads; disabled pending decision.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
