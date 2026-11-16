#!/usr/bin/env python3
"""Current proof of pending talent candidates, without opening learned gates."""
import argparse
from collections import Counter
import hashlib
import re
import subprocess
import sys
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot

ROOT = e.ROOT
STAGE = ROOT / '.local/class-skills/warlock-remaining-review'
PARENT = ROOT / '.local/class-skills/cold-parameter'
sys.path.insert(0, str(ROOT / 'tests'))
from check_warlock_remaining_review import configuration, FAMILIES

FROZEN = (
    ('blood-oath-damage', 'blood-oath-damage'),
    ('engorge-world-rule', 'engorge-world'),
    ('blood-boil-rule', 'blood-boil-world'),
    ('consume-world-rule', 'consume-world'))


def expected_markers():
    result = Counter({'iron-golem-birth-check': 3})
    for family in ('iron-golem-items', 'npc-resource-callbacks', *FAMILIES):
        for suffix, count in (('-check', 1), ('-reload-check', 2)):
            result[family + suffix] += count
            if family in FAMILIES:
                result[family + suffix + '-result'] += count
    assert sum(result.values()) == 75
    return result


def record(check=False):
    snapshot(PARENT / 'source-snapshot-4.json', STAGE / 'birth-inputs.json')
    born = e.load(STAGE / 'birth-inputs.json')
    parent = e.load(PARENT / 'birth-inputs.json')
    assert born['artifacts'] == parent['artifacts']
    assert born['copiedData'] == parent['copiedData']
    assert {p: h for p, h in born['sourceInputs'].items() if p.endswith('.cs')} == {
        p: h for p, h in parent['sourceInputs'].items() if p.endswith('.cs')}
    for p, h in born['sourceInputs'].items():
        assert e.sha(ROOT / p) == h, p
    for p, h in born['copiedData'].items():
        assert e.sha(ROOT / 'data' / p.removeprefix('Data/')) == h, p
    for p in ('ModernUO.dll', 'Server.dll', 'Assemblies/UOContent.dll'):
        assert e.sha(STAGE / 'native/Distribution' / p) == born['artifacts'][p]
    for kind in ('Content', 'Integration'):
        assert e.sha(STAGE / kind.lower() / f'Sanctuary.{kind}.dll') == born['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
    frozen = {}
    files = []
    for proposal, receipt in FROZEN:
        for p in (f'docs/{proposal}-proposal.md', f'tools/reference/{receipt}-native-evidence.json'):
            before = subprocess.check_output(['git', 'show', 'd197f373:' + p], cwd=ROOT)
            assert hashlib.sha256(before).hexdigest() == e.sha(ROOT / p), p
            frozen[p] = e.sha(ROOT / p)
            files.append(ROOT / p)
    implementation = (ROOT / 'src/Sanctuary.Content/WarlockCombat.cs').read_text().split('public static bool Implemented(', 1)[1].split(';', 1)[0]
    assert implementation.count('CombatAbility.') == 26
    assert all('CombatAbility.' + n not in implementation for n in ('BloodOath', 'Engorge', 'BloodBoil', 'Consume'))
    assert 'Approved=>false' in (ROOT / 'src/Sanctuary.Content/BloodOathDamageProposal.cs').read_text()
    assert 'Approved=>false' in (ROOT / 'src/Sanctuary.Content/BloodBoilPrepared.cs').read_text()
    log = e.cohort(STAGE, expected_markers(), 75)
    assert log.count('Client: 127.0.0.1: Connected.') == 33 and 'denied by connection filter' not in log
    assert 'pass across3boots/2reloads' in (STAGE / 'native-run-1.log').read_text()
    unsupported = [line for line in log.splitlines() if ' WRN]' in line and 'Blood Oath refused unsupported incoming context' in line]
    assert len(unsupported) == 3
    files += [ROOT / 'tools/record_warlock_remaining_review.py', ROOT / 'docs/warlock-final-talents-release-proposal.md',
        ROOT / 'docs/warlock-remaining-review-checkpoint.md', ROOT / 'tests/check_iron_golem_birth.py',
        ROOT / 'tests/check_britain.py', PARENT / 'source-snapshot-4.json',
        STAGE / 'birth-inputs.json', STAGE / 'birth-native-checks.log', STAGE / 'native-run-1.log']
    files += list(configuration()['extra_inputs'])
    result = dict(component='Current combined review of four pending Warlock talents', referenceVersion='3.3.93847',
        nativeMarkers=75, markers=dict(sorted(expected_markers().items())), families=list(FAMILIES),
        actualWireAbilityInputs=0, connections=33, boots=3, saves=3, reloads=2,
        artifacts=born['artifacts'], artifactReuse='Exact cold-parameter C4/I4/native0075 artifacts',
        nativeRebuilt=False, contentRebuilt=False, integrationRebuilt=False,
        csharpChanged=False, runtimeDataChanged=False, frozenProposalsAndReceipts=frozen,
        learnedGatesOpened=False, approved=False, installed=False, classCertified=False, goalComplete=False,
        saveDeletionWarnings=re.findall(r'Warning: Attempted to delete (.+) during world save\.', log),
        expectedUnsupportedOathWarnings=unsupported,
        protection=e.protection('tools/reference/slow-missiles-learned-client-evidence.json', 44),
        limits='Prepared world/callback/supplier and qualified bound/aura composition only. Four rules remain undecided;26Warlock/236global handlers,zero learned ability socket inputs here. No hidden callback parity or whole-class claim. A release decision must include the separately prepared bound/aura extensions;actual learned admission follows one talent at a time. No current D2R reads,charms/runewords/assets/deployment.')
    e.write('warlock-remaining-review-native-evidence.json', result, files, check)
    print('Pending Warlock review:75 results/33connections/two reloads;four proposals and learned gates unchanged.')


if __name__ == '__main__':
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--check', action='store_true')
    record(p.parse_args().check)
