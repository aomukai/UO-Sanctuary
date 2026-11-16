#!/usr/bin/env python3
"""Bind approved terrain admission to actual ground inputs and retained saves."""
import argparse
from collections import Counter
import json
import re
import subprocess
import sys

import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot

ROOT = e.ROOT
STAGE = ROOT / '.local/class-skills/explosive-terrain-adopted'
PREPARED = ROOT / '.local/class-skills/explosive-terrain'
sys.path.insert(0, str(ROOT / 'tests'))
from check_explosive_terrain_adopted import configuration, WALL_INPUTS


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
    assert sum(expected.values()) == 70
    return expected


def record(check=False):
    run = STAGE / 'native-run-2.log'
    assert 'pass across3boots/2reloads' in run.read_text()
    decision_path = ROOT / 'tools/reference/explosive-terrain-rule-approval.json'
    decision = e.load(decision_path)
    assert decision['approved'] and decision['referenceVersion'] == '3.3.93847'
    assert e.sha(ROOT / decision['proposal']) == decision['proposalAtApprovalSha256']
    assert e.sha(ROOT / decision['testedCandidateEvidence']) == decision['testedCandidateEvidenceSha256']
    prior = e.load(ROOT / decision['testedCandidateEvidence'])
    parent = e.load(PREPARED / 'birth-inputs.json')
    assert prior['artifacts'] == parent['artifacts']
    for kind in ('Content', 'Integration'):
        assert e.sha(PREPARED / kind.lower() / f'Sanctuary.{kind}.dll') == parent['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
    born = e.load(STAGE / 'birth-inputs.json')
    snapshot(STAGE / 'source-snapshot-3.json', STAGE / 'birth-inputs.json')
    for name, digest in born['sourceInputs'].items():
        assert e.sha(ROOT / name) == digest, name
    source_names = {name for name in born['sourceInputs'] if name.endswith('.cs')}
    changed = {name for name in source_names if born['sourceInputs'][name] != parent['sourceInputs'].get(name)}
    assert changed == {'src/Sanctuary.Content/SorceressSpellMissiles.cs',
        *('tests/Sanctuary.Integration/' + name + '.cs' for name in (
            'FireBallChecks', 'GlacialSpikeChecks', 'ExplosiveTerrainChecks'))}
    for kind, number in (('src/', 1), ('shared/', 1), ('tests/', 2)):
        compiled = e.load(STAGE / f'source-snapshot-{number}.json')['inputs']['sourceInputs']
        for name in source_names:
            if name.startswith(kind):
                assert born['sourceInputs'][name] == compiled[name], name
    metadata_path = 'Data/Sanctuary/Balance/class-skill-status.json'
    assert {name for name, digest in born['copiedData'].items()
        if digest != parent['copiedData'][name]} == {metadata_path}
    old = json.loads(subprocess.check_output(['git', 'show',
        decision['candidateCommit'] + ':data/Sanctuary/Balance/class-skill-status.json'], cwd=ROOT))
    current = e.load(ROOT / 'data/Sanctuary/Balance/class-skill-status.json')
    affected = {'sorceress.fireball', 'sorceress.glacialspike'}
    for before, after in zip(old['skills'], current['skills'], strict=True):
        if before['talentId'] in affected:
            assert before['remaining'] != after['remaining']
            assert before | {'remaining': after['remaining']} == after
        else:
            assert before == after
    assert old | {'skills': current['skills']} == current
    for name, digest in born['copiedData'].items():
        assert e.sha(ROOT / 'data' / name.removeprefix('Data/')) == digest, name
    for name in ('ModernUO.dll', 'Server.dll', 'Assemblies/UOContent.dll'):
        assert e.sha(STAGE / 'native/Distribution' / name) == born['artifacts'][name] == parent['artifacts'][name]
    for kind in ('Content', 'Integration'):
        assert e.sha(STAGE / kind.lower() / f'Sanctuary.{kind}.dll') == born['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
    for name in ('content-build-1.log', 'integration-build-2.log'):
        log = (STAGE / name).read_text()
        assert all(value in log for value in ('Build succeeded.', '0 Warning(s)', '0 Error(s)'))
    source = (ROOT / 'src/Sanctuary.Content/SorceressSpellMissiles.cs').read_text()
    assert 'internal static bool ExplosiveTerrainApproved=>true;' in source
    assert 'PreparedExplosiveTerrain' not in source
    assert 'ExplosiveTerrainApproved&&flight.Npc==null' in source
    log = e.cohort(STAGE, expected_markers(), 70)
    assert log.count('Client: 127.0.0.1: Connected.') == 28 and 'denied by connection filter' not in log
    files = [ROOT / 'tools/record_explosive_terrain_adopted.py',
        ROOT / 'docs/explosive-terrain-adopted-checkpoint.md', decision_path,
        ROOT / decision['testedCandidateEvidence'], ROOT / decision['proposal'],
        ROOT / 'tools/build_skill_roadmap.py', ROOT / 'data/Sanctuary/Balance/class-skill-status.json',
        STAGE / 'source-snapshot-1.json', STAGE / 'source-snapshot-2.json',
        STAGE / 'source-snapshot-3.json', STAGE / 'content-build-1.log',
        STAGE / 'integration-build-2.log', STAGE / 'birth-inputs.json',
        STAGE / 'birth-native-checks.log', run, STAGE / 'failed-run-1/birth-inputs.json',
        STAGE / 'failed-run-1/birth-native-checks.log']
    files += list(configuration()['extra_inputs'])
    result = dict(component='Approved player Fire Ball/Glacial native wall mapping',
        referenceVersion='3.3.93847', approved=True, defaultRuleEnabled=True,
        nativeMarkers=70, markers=dict(sorted(expected_markers().items())),
        actualWireInputs=24, dedicatedWallWireInputs=3*len(WALL_INPUTS), connections=28,
        directionalWalls=16, openCases=4, dynamicDoorCases=4, resumedCopies=4,
        boots=3, saves=3, reloads=2, contentBuild=1, integrationBuild=2,
        sourceSnapshot=3, nativeRun=2, artifacts=born['artifacts'],
        changedSources=sorted(changed), gameplayChanged=True, persistedSchemaChanged=False,
        dataDelta='Only two remaining-description strings in class-skill-status.json',
        installed=False, classCertified=False, goalComplete=False,
        saveDeletionWarnings=re.findall(r'Warning: Attempted to delete (.+) during world save\.', log),
        unsupportedWarnings=[line for line in log.splitlines() if ' WRN]' in line and 'unsupported' in line.lower()],
        protection=e.protection('tools/reference/slow-missiles-learned-client-evidence.json', 44),
        limits='Approved native terrain adaptation, not recovered exact D2R coordinates. NPC/mage and other explosive controllers remain unchanged; broader room/recipient/PvP/graphical and final effects/icons stay explicit. Two WardrobeSet save warnings remain; no charms/runewords or installed changes.')
    e.write('explosive-terrain-adopted-native-evidence.json', result, files, check)
    print('Approved explosive terrain:70 results/24actual inputs/2reloads; default mapping enabled.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
