#!/usr/bin/env python3
"""Record the ten-route Warlock Chaos/Sigil group without parity certification."""
import argparse
from collections import Counter
import re
import sys

import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot

ROOT = e.ROOT
STAGE = ROOT / '.local/class-skills/warlock-chaos-acceptance'
sys.path.insert(0, str(ROOT / 'tests'))
from check_warlock_chaos_acceptance import configuration, ROUTES, FAMILIES, INPUTS, CONNECTED, CHECKS


def expected_markers():
    config = configuration()
    expected = Counter({'iron-golem-birth-check': 3})
    expected.update({command: 3 for command in CHECKS})
    for family in ('iron-golem-items', 'npc-resource-callbacks', *FAMILIES):
        for suffix, count in (('-check', 1), ('-reload-check', 2)):
            command = family + suffix
            expected[command] += count
            if command in config['extra_wait_results']:
                expected[config['extra_wait_results'][command]] += count
    for command in INPUTS:
        expected[command + '-check'] += 3
        expected[command + '-result'] += 3
    assert sum(expected.values()) == 315
    return expected


def record(check=False):
    run = STAGE / 'native-run-2.log'
    assert 'pass across3boots/2reloads' in run.read_text(), 'Chaos/Sigil group has not succeeded'
    born = e.load(STAGE / 'birth-inputs.json')
    snapshot(STAGE / 'source-snapshot-2.json', STAGE / 'birth-inputs.json')
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
    before = e.load(STAGE / 'source-snapshot-1.json')['inputs']['sourceInputs']
    changed = {name for name, digest in before.items() if digest != born['sourceInputs'][name]}
    assert changed == {'tests/Sanctuary.Integration/AbyssCastChecks.cs'}
    build = (STAGE / 'integration-build-1.log').read_text()
    assert all(value in build for value in ('Build succeeded.', '0 Warning(s)', '0 Error(s)'))
    for kind in ('Content', 'Integration'):
        assert e.sha(STAGE / kind.lower() / f'Sanctuary.{kind}.dll') == born['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
    for name in ('ModernUO.dll', 'Server.dll', 'Assemblies/UOContent.dll'):
        assert e.sha(STAGE / 'native/Distribution' / name) == born['artifacts'][name]
    log = e.cohort(STAGE, expected_markers(), 315)
    connections = 3 * (len(CONNECTED) + len(INPUTS))
    assert connections == 135 and log.count('Client: 127.0.0.1: Connected.') == connections
    assert 'denied by connection filter' not in log
    config = configuration()
    assert (len(ROUTES), len(FAMILIES), len(CHECKS), len(INPUTS), len(config['extra_files'])) == (10, 28, 2, 27, 5)
    files = [ROOT / 'tools/record_warlock_chaos_acceptance.py',
        ROOT / 'tests/check_warlock_chaos_acceptance.py',
        ROOT / 'docs/warlock-chaos-acceptance-checkpoint.md',
        STAGE / 'artifact-reuse.json', STAGE / 'source-snapshot-1.json',
        STAGE / 'source-snapshot-2.json', STAGE / 'integration-build-1.log',
        STAGE / 'failed-run-1/birth-inputs.json', STAGE / 'failed-run-1/birth-native-checks.log',
        STAGE / 'birth-inputs.json', STAGE / 'birth-native-checks.log', run]
    files += list(config['extra_inputs'])
    result = dict(component='Warlock Chaos/Sigil acceptance group',
        referenceVersion='3.3.93847', routes=list(ROUTES), routesCovered=10,
        priorRoutesAccepted=16, implementedRoutesCovered=26, frozenPending=4,
        nativeMarkers=315, markers=dict(sorted(expected_markers().items())),
        fixtureFamilies=28, sharedChecks=2, currentInputAttempts=81, connections=135,
        boots=3, saves=3, reloads=2, contentBuildReused=1, parentIntegrationBuild=9, integrationBuild=1,
        sourceSnapshot=2, nativeRun=2, changedSources=sorted(changed), artifacts=born['artifacts'],
        gameplayChanged=False, runtimeDataChanged=False, csharpChanged=True,
        installed=False, classCertified=False, goalComplete=False,
        saveDeletionWarnings=re.findall(r'Warning: Attempted to delete (.+) during world save\.', log),
        unsupportedWarnings=[line for line in log.splitlines() if ' WRN]' in line and 'unsupported' in line.lower()],
        protection=e.protection('tools/reference/slow-missiles-learned-client-evidence.json', 44),
        limits='Scoped existing Chaos/Sigil controls with approved adaptations; first-run Abyss wire timeout remains unexplained, diagnostic-only change retains original assertions; no full modern-parity or class certificate. Four proposals remain frozen. Current-engine/native/item/recipient/terrain/PvP/graphical and final effects/audio/icons remain explicit; charms/runewords excluded.')
    e.write('warlock-chaos-acceptance-native-evidence.json', result, files, check)
    print('Warlock Chaos/Sigil group:315 exact results/10routes/3saves/2reloads.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
