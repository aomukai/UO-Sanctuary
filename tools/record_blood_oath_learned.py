#!/usr/bin/env python3
"""Bind default learned Blood Oath, without temporary test admission."""
import argparse
from collections import Counter
import json
import re
import subprocess
import sys
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot

ROOT = e.ROOT
STAGE = ROOT / '.local/class-skills/blood-oath-learned'
sys.path.insert(0, str(ROOT / 'tests'))
from check_blood_oath_learned import configuration


def expected_markers():
    c = configuration()
    result = Counter({'iron-golem-birth-check': 3})
    for family in ('iron-golem-items', 'npc-resource-callbacks', *c['extra_families']):
        for suffix, count in (('-check', 1), ('-reload-check', 2)):
            command = family + suffix
            result[command] += count
            if command in c['extra_wait_results']:
                result[c['extra_wait_results'][command]] += count
    for command in c['extra_checks']:
        result[command] += 3
        result[c['extra_wait_results'][command]] += 3
    assert sum(result.values()) == 27
    return result


def record(check=False):
    approval = e.load(ROOT / 'tools/reference/warlock-final-talents-approval.json')
    assert approval['approved'] and 'warlock.bloodoath' in approval['approvedTalents']
    for p, h in approval['filesAtApprovalSha256'].items():
        assert e.sha(ROOT / p) == h, p
    born = e.load(STAGE / 'birth-inputs.json')
    snapshot(STAGE / 'source-snapshot-3.json', STAGE / 'birth-inputs.json')
    parent = e.load(ROOT / '.local/class-skills/warlock-remaining-review/birth-inputs.json')
    changed = {p for p, h in born['sourceInputs'].items() if p.endswith('.cs') and parent['sourceInputs'].get(p) != h}
    assert changed == {'src/Sanctuary.Content/BloodOathDamageProposal.cs', 'src/Sanctuary.Content/WarlockCombat.cs',
        'tests/Sanctuary.Integration/BloodOathDamageChecks.cs', 'tests/Sanctuary.Integration/BoundDemonOathChecks.cs',
        'tests/Sanctuary.Integration/BloodOathLearnedChecks.cs'}
    for p, h in born['sourceInputs'].items():
        assert e.sha(ROOT / p) == h, p
    assert {p for p, h in born['copiedData'].items() if h != parent['copiedData'][p]} == {'Data/Sanctuary/Balance/class-skill-status.json'}
    old = json.loads(subprocess.check_output(['git', 'show', 'a1d4bc47:data/Sanctuary/Balance/class-skill-status.json'], cwd=ROOT))
    current = e.load(ROOT / 'data/Sanctuary/Balance/class-skill-status.json')
    changed_rows = [a['talentId'] for a, b in zip(old['skills'], current['skills'], strict=True) if a != b]
    assert changed_rows == ['warlock.bloodoath'] and old | {'skills': current['skills']} == current
    assert sum(r['implementation'] == 'handler-present' for r in current['skills']) == 237
    for p, h in born['copiedData'].items():
        assert e.sha(ROOT / 'data' / p.removeprefix('Data/')) == h, p
    for p in ('ModernUO.dll', 'Server.dll', 'Assemblies/UOContent.dll'):
        assert e.sha(STAGE / 'native/Distribution' / p) == born['artifacts'][p] == parent['artifacts'][p]
    for kind in ('Content', 'Integration'):
        assert e.sha(STAGE / kind.lower() / f'Sanctuary.{kind}.dll') == born['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
        build = (STAGE / (kind.lower() + '-build-3.log')).read_text()
        assert all(v in build for v in ('Build succeeded.', '0 Warning(s)', '0 Error(s)'))
    source = (ROOT / 'src/Sanctuary.Content/BloodOathDamageProposal.cs').read_text()
    assert 'Approved=>true' in source and 'AdmitPrepared' not in source and 'Prepared.ContainsKey' not in source
    log = e.cohort(STAGE, expected_markers(), 27)
    assert 'pass across3boots/2reloads' in (STAGE / 'native-run-3.log').read_text()
    assert log.count('Client: 127.0.0.1: Connected.') == 9 and 'denied by connection filter' not in log
    refused = [line for line in log.splitlines() if ' WRN]' in line and 'Blood Oath refused unsupported incoming context' in line]
    assert len(refused) == 3
    files = [ROOT / 'tools/record_blood_oath_learned.py', ROOT / 'docs/blood-oath-learned-checkpoint.md',
        ROOT / 'tools/reference/warlock-final-talents-approval.json', ROOT / 'tools/build_skill_roadmap.py',
        ROOT / 'data/Sanctuary/Balance/class-skill-status.json', ROOT / 'tests/check_iron_golem_birth.py', ROOT / 'tests/check_britain.py',
        STAGE / 'source-snapshot-3.json', STAGE / 'birth-inputs.json', STAGE / 'birth-native-checks.log',
        STAGE / 'native-run-3.log', STAGE / 'content-build-3.log', STAGE / 'integration-build-3.log',
        STAGE / 'failed-build-1.tar.gz', STAGE / 'failed-run-2.tar.gz']
    files += list(configuration()['extra_inputs'])
    result = dict(component='Automatic learned Blood Oath original/bound passive', referenceVersion='3.3.93847',
        approved=True, defaultRuleEnabled=True, temporaryAdmissionRemoved=True,
        nativeMarkers=27, markers=dict(sorted(expected_markers().items())), connections=9,
        actualActiveAbilityWireInputs=0, learnedPassiveControlBoots=3, boots=3, saves=3, reloads=2,
        globalHandlers=237, warlockHandlers=27, remainingApprovedTalents=['warlock.engorge', 'warlock.bloodboil', 'warlock.consume'],
        contentBuild=3, integrationBuild=3, sourceSnapshot=3, nativeRun=3, artifacts=born['artifacts'],
        changedSources=sorted(changed), runtimeDataChange='Blood Oath handler/verification/remaining row only',
        nativeRebuilt=False, persistedSchemaChanged=False, installed=False, classCertified=False, goalComplete=False,
        expectedUnsupportedWarnings=refused,
        saveDeletionWarnings=re.findall(r'Warning: Attempted to delete (.+) during world save\.', log),
        protection=e.protection('tools/reference/slow-missiles-learned-client-evidence.json', 44),
        limits='Passive training uses the normal service in authenticated native sessions;no active cast is applicable and no graphical talent-purchase proof is claimed. Current event35/start66/global ordering and unqualified native modifiers/equipment/recipient/PvP/client contexts remain explicit. Original/bound rules are approved adaptations;three approved active talents still need admission. Existing passive icon/transfer/audio gaps remain separate;no charms/runewords/assets/deployment.')
    e.write('blood-oath-learned-native-evidence.json', result, files, check)
    print('Learned Blood Oath:27 results/9connections/two reloads;237global/27Warlock handlers,three approved routes next.')


if __name__ == '__main__':
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--check', action='store_true')
    record(p.parse_args().check)
