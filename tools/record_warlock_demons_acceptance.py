#!/usr/bin/env python3
"""Record Warlock's first seven routes without certifying the unfinished class."""
import argparse
from collections import Counter
import re
import sys

import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot

ROOT = e.ROOT
STAGE = ROOT / '.local/class-skills/warlock-planned-acceptance'
sys.path.insert(0, str(ROOT / 'tests'))
from check_warlock_demons_acceptance import configuration, ROUTES, FROZEN, CONNECTED, INPUTS


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
    for command in INPUTS:
        expected[command + '-check'] += 3
        expected[command + '-result'] += 3
    for source in (1, 9):
        expected[f'bind-learned-wire-{source}-result'] += 3
    expected[config['save_result']] += 3
    assert sum(expected.values()) == 180
    return expected


def record(check=False):
    run = STAGE / 'demons-native-run-1.log'
    assert 'pass across3boots/2reloads' in run.read_text(), 'Native group has not succeeded'
    born = e.load(STAGE / 'birth-inputs.json')
    snapshot(STAGE / 'source-snapshot-1.json', STAGE / 'birth-inputs.json')
    for name, digest in born['sourceInputs'].items():
        assert e.sha(ROOT / name) == digest, name
    for name, digest in born['copiedData'].items():
        assert e.sha(ROOT / 'data' / name.removeprefix('Data/')) == digest, name
    reuse = e.load(STAGE / 'artifact-reuse.json')
    assert e.sha(ROOT / reuse['parentCheckpoint']) == reuse['parentCheckpointSha256']
    assert e.sha(ROOT / reuse['parentNativeInputs']) == reuse['parentNativeInputsSha256']
    assert born['artifacts'] == reuse['artifacts'] and born['copiedData'] == reuse['copiedData']
    for kind in ('Content', 'Integration'):
        assert e.sha(STAGE / kind.lower() / f'Sanctuary.{kind}.dll') == born['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
    for name in ('ModernUO.dll', 'Server.dll', 'Assemblies/UOContent.dll'):
        assert e.sha(STAGE / 'native/Distribution' / name) == born['artifacts'][name]
    log = e.cohort(STAGE, expected_markers(), 180)
    connections = 3 * (len(CONNECTED) + len(INPUTS) + 2 + 1)
    assert connections == 75 and log.count('Client: 127.0.0.1: Connected.') == connections
    assert 'denied by connection filter' not in log
    config = configuration()
    assert len(config['extra_families']) == 21 and len(config['extra_files']) == 4
    files = [ROOT / 'tools/record_warlock_demons_acceptance.py',
        ROOT / 'tests/check_warlock_demons_acceptance.py',
        STAGE / 'source-snapshot-1.json', STAGE / 'artifact-reuse.json',
        STAGE / 'birth-inputs.json', STAGE / 'birth-native-checks.log', run,
        ROOT / 'docs/warlock-demons-acceptance-checkpoint.md',
        ROOT / 'docs/skill-effects.md', ROOT / 'docs/skill-icons.md']
    files += list(config['extra_inputs'])
    result = dict(component='Warlock/Witch demon and foundation acceptance group',
        referenceVersion='3.3.93847', routes=list(ROUTES), frozenPending=list(FROZEN),
        routesCovered=7, otherImplementedRoutesRemaining=19, fixtureFamilies=21,
        sharedChecks=1, nativeMarkers=180, markers=dict(sorted(expected_markers().items())),
        currentInputAttempts=27, bindWireSequences=6, connections=75,
        boots=3, saves=3, reloads=2, contentBuildReused=1,
        integrationBuildReused=15, sourceSnapshot=1, nativeRun=1,
        artifacts=born['artifacts'], gameplayChanged=False, runtimeDataChanged=False,
        csharpChanged=False, installed=False, classCertified=False, goalComplete=False,
        saveDeletionWarnings=re.findall(r'Warning: Attempted to delete (.+) during world save\.', log),
        unsupportedWarnings=[line for line in log.splitlines() if ' WRN]' in line and 'unsupported' in line.lower()],
        protection=e.protection('tools/reference/slow-missiles-learned-client-evidence.json', 44),
        limits='First seven routes only; remaining weapon/Hex/Ward/Sigil/Chaos routes and four frozen proposals are not claimed complete. Current executable, special native species/elite/Sunder, item/recipient/terrain/PvP, graphical and final effect/audio/icon limits remain explicit. Existing component controls are reused without new reference compilation or runtime changes.')
    e.write('warlock-demons-acceptance-native-evidence.json', result, files, check)
    print('Warlock demons/foundations:180 exact results/7routes/3saves/2reloads.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
