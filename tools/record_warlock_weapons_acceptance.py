#!/usr/bin/env python3
"""Record the nine-route Warlock weapon/Hex/Ward group without whole-class claims."""
import argparse
from collections import Counter
import re
import sys

import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot

ROOT = e.ROOT
STAGE = ROOT / '.local/class-skills/warlock-weapons-acceptance'
sys.path.insert(0, str(ROOT / 'tests'))
from check_warlock_weapons_acceptance import configuration, ROUTES, FAMILIES, INPUTS, CONNECTED


def expected_markers():
    config = configuration()
    expected = Counter({'iron-golem-birth-check': 3})
    for family in ('iron-golem-items', 'npc-resource-callbacks', *FAMILIES):
        for suffix, count in (('-check', 1), ('-reload-check', 2)):
            command = family + suffix
            expected[command] += count
            if command in config['extra_wait_results']:
                expected[config['extra_wait_results'][command]] += count
    for command in INPUTS:
        expected[command + '-check'] += 3
        expected[command + '-result'] += 3
    expected['cleave-cast-presave-check'] += 3
    assert sum(expected.values()) == 240
    return expected


def record(check=False):
    run = STAGE / 'native-run-7.log'
    assert 'pass across3boots/2reloads' in run.read_text(), 'Weapon group has not succeeded'
    born = e.load(STAGE / 'birth-inputs.json')
    snapshot(STAGE / 'source-snapshot-10.json', STAGE / 'birth-inputs.json')
    for name, digest in born['sourceInputs'].items():
        assert e.sha(ROOT / name) == digest, name
    for name, digest in born['copiedData'].items():
        assert e.sha(ROOT / 'data' / name.removeprefix('Data/')) == digest, name
    reuse = e.load(STAGE / 'artifact-reuse.json')
    assert e.sha(ROOT / reuse['parentCheckpoint']) == reuse['parentCheckpointSha256']
    assert e.sha(ROOT / reuse['parentNativeInputs']) == reuse['parentNativeInputsSha256']
    assert born['copiedData'] == reuse['copiedData']
    for name, digest in born['artifacts'].items():
        if name != 'Assemblies/Sanctuary.Integration.dll':
            assert digest == reuse['artifacts'][name], name
    for kind in ('Content', 'Integration'):
        assert e.sha(STAGE / kind.lower() / f'Sanctuary.{kind}.dll') == born['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
    before = e.load(STAGE / 'source-snapshot-1.json')['inputs']['sourceInputs']
    changed = {name for name, digest in before.items() if digest != born['sourceInputs'][name]}
    assert changed == {'tests/Sanctuary.Integration/' + name + '.cs'
        for name in ('WeaponHexGroupChecks', 'HexPurgeCastChecks', 'HexBaneCastChecks', 'CleaveActionChecks', 'CleaveDirectionChecks', 'EldritchBlastCastChecks')}
    build = (STAGE / 'integration-build-9.log').read_text()
    assert all(value in build for value in ('Build succeeded.', '0 Warning(s)', '0 Error(s)'))
    focused = e.load(ROOT / 'tools/reference/warlock-hex-registration-native-evidence.json')
    assert focused['nativeMarkers'] == 66
    for name, digest in focused['artifacts'].items():
        if name != 'Assemblies/Sanctuary.Integration.dll':
            assert digest == born['artifacts'][name], name
    for name, digest in focused['files'].items():
        path = STAGE / 'failed-run-2/sources' / name if name.endswith('.cs') and not name.startswith('.local/') else ROOT / name
        assert e.sha(path) == digest, name
    space = e.load(ROOT / 'tools/reference/warlock-fixture-space-native-evidence.json')
    assert space['nativeMarkers'] == 18
    assert space['artifacts'] == e.load(STAGE / 'failed-run-3/birth-inputs.json')['artifacts']
    for name, digest in space['artifacts'].items():
        if name != 'Assemblies/Sanctuary.Integration.dll':
            assert digest == born['artifacts'][name], name
    for name, digest in space['files'].items():
        assert e.sha(ROOT / name) == digest, name
    log = e.cohort(STAGE, expected_markers(), 240)
    connections = 3 * (len(CONNECTED) + len(INPUTS))
    assert connections == 93 and log.count('Client: 127.0.0.1: Connected.') == connections
    assert 'denied by connection filter' not in log
    config = configuration()
    assert (len(FAMILIES), len(ROUTES), len(INPUTS), len(config['extra_files'])) == (21, 9, 24, 4)
    files = [ROOT / 'tools/record_warlock_weapons_acceptance.py',
        ROOT / 'tests/check_warlock_weapons_acceptance.py',
        ROOT / 'tools/reference/warlock-hex-registration-native-evidence.json',
        ROOT / 'tools/reference/warlock-fixture-space-native-evidence.json',
        ROOT / 'docs/warlock-weapons-acceptance-checkpoint.md',
        STAGE / 'birth-inputs.json', STAGE / 'birth-native-checks.log', run,
        STAGE / 'artifact-reuse.json', STAGE / 'source-snapshot-1.json',
        STAGE / 'source-snapshot-10.json', STAGE / 'integration-build-9.log',
        STAGE / 'failed-run-1/birth-inputs.json', STAGE / 'failed-run-1/birth-native-checks.log',
        STAGE / 'failed-run-2/birth-inputs.json', STAGE / 'failed-run-2/birth-native-checks.log',
        STAGE / 'failed-run-3/birth-inputs.json', STAGE / 'failed-run-3/birth-native-checks.log',
        STAGE / 'failed-run-4/birth-inputs.json', STAGE / 'failed-run-4/birth-native-checks.log',
        STAGE / 'failed-run-5/birth-inputs.json', STAGE / 'failed-run-5/birth-native-checks.log',
        STAGE / 'failed-run-6/birth-inputs.json', STAGE / 'failed-run-6/birth-native-checks.log']
    files += list(config['extra_inputs'])
    result = dict(component='Warlock weapon/Hex/Ward acceptance group',
        referenceVersion='3.3.93847', routes=list(ROUTES), routesCovered=9,
        priorDemonRoutesAccepted=7, remainingChaosRoutes=10, frozenPending=4,
        nativeMarkers=240, markers=dict(sorted(expected_markers().items())),
        fixtureFamilies=21, currentInputAttempts=72, connections=93,
        boots=3, saves=3, reloads=2, contentBuildReused=1, integrationBuild=9,
        sourceSnapshot=10, nativeRun=7, changedSources=sorted(changed),
        artifacts=born['artifacts'], gameplayChanged=False, runtimeDataChanged=False,
        installed=False, classCertified=False, goalComplete=False,
        saveDeletionWarnings=re.findall(r'Warning: Attempted to delete (.+) during world save\.', log),
        unsupportedWarnings=[line for line in log.splitlines() if ' WRN]' in line and 'unsupported' in line.lower()],
        protection=e.protection('tools/reference/slow-missiles-learned-client-evidence.json', 44),
        limits='Nine routes plus existing shared Magic Arrow regression. Bane saved-life loss remains unattributed; retained target is parked between controls, with exact life/state and current diagnostics retained. Historical Hex66 source hashes bind frozen N2 sources; its receipt is not regenerated. Current-engine/item/recipient/terrain/PvP/graphical and final effects/audio/icons stay explicit. Ten Chaos/Sigil routes and four frozen proposals are not covered or claimed complete.')
    e.write('warlock-weapons-acceptance-native-evidence.json', result, files, check)
    print('Warlock weapon/Hex/Ward group:240 exact results/9routes/3saves/2reloads.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
