#!/usr/bin/env python3
"""Record approved default self damage and its actual learned-input cohort."""
import argparse
from collections import Counter
import json
import re
import subprocess
import sys
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot

ROOT = e.ROOT
STAGE = ROOT / '.local/class-skills/fanaticism-mana-adopted'
sys.path.insert(0, str(ROOT / 'tests'))
from check_fanaticism_mana_adopted import configuration


def expected_markers():
    c = configuration()
    result = Counter({'iron-golem-birth-check': 3, 'offensive-aura-check': 3})
    for family in ('iron-golem-items', 'npc-resource-callbacks', *c['extra_families']):
        for suffix, count in (('-check', 1), ('-reload-check', 2)):
            command = family + suffix
            result[command] += count
            if command in c['extra_wait_results']:
                result[c['extra_wait_results'][command]] += count
    for command in c['extra_target_input_commands']:
        result[command + '-check'] += 3
        result[command + '-result'] += 3
    assert sum(result.values()) == 47
    return result


def record(check=False):
    approval = e.load(ROOT / 'tools/reference/fanaticism-mana-approval.json')
    assert approval['approved'] and approval['answer'] == 'Adopt mana-neutral personal damage (Recommended)'
    assert e.sha(ROOT / approval['proposal']) == approval['proposalAtApprovalSha256']
    assert e.sha(ROOT / approval['testedCandidateEvidence']) == approval['testedCandidateEvidenceSha256']
    born = e.load(STAGE / 'birth-inputs.json')
    snapshot(STAGE / 'source-snapshot-1.json', STAGE / 'birth-inputs.json')
    prior = e.load(ROOT / '.local/class-skills/iron-golem-fireball/birth-inputs.json')
    changed = {p for p, h in born['sourceInputs'].items() if p.endswith('.cs') and prior['sourceInputs'].get(p) != h}
    assert changed == {'src/Sanctuary.Content/PaladinAuras.cs'} | {
        f'tests/Sanctuary.Integration/{n}Checks.cs' for n in ('FanaticismMana', 'MightAura', 'OffensiveAura', 'PaladinOffense')}
    for p, h in born['sourceInputs'].items():
        assert e.sha(ROOT / p) == h, p
    assert {p for p, h in born['copiedData'].items() if h != prior['copiedData'][p]} == {'Data/Sanctuary/Balance/class-skill-status.json'}
    old = json.loads(subprocess.check_output(['git', 'show', '44ffb282:data/Sanctuary/Balance/class-skill-status.json'], cwd=ROOT))
    current = e.load(ROOT / 'data/Sanctuary/Balance/class-skill-status.json')
    for a, b in zip(old['skills'], current['skills'], strict=True):
        assert a | {'remaining': b['remaining']} == b if a['talentId'] == 'paladin.fanaticism' else a == b
    assert old | {'skills': current['skills']} == current
    for p, h in born['copiedData'].items():
        assert e.sha(ROOT / 'data' / p.removeprefix('Data/')) == h, p
    for p in ('ModernUO.dll', 'Server.dll', 'Assemblies/UOContent.dll'):
        assert e.sha(STAGE / 'native/Distribution' / p) == born['artifacts'][p] == prior['artifacts'][p]
    for kind in ('Content', 'Integration'):
        assert e.sha(STAGE / kind.lower() / f'Sanctuary.{kind}.dll') == born['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
        build = (STAGE / (kind.lower() + '-build-1.log')).read_text()
        assert all(v in build for v in ('Build succeeded.', '0 Warning(s)', '0 Error(s)'))
    source = (ROOT / 'src/Sanctuary.Content/PaladinAuras.cs').read_text()
    assert 'PreparedFanaticismManaNeutral' not in source and 'fullSelfDamage||values.Mana256==0' in source
    log = e.cohort(STAGE, expected_markers(), 47)
    assert log.count('Client: 127.0.0.1: Connected.') == 14 and 'denied by connection filter' not in log
    assert 'pass across3boots/2reloads' in (STAGE / 'native-run-1.log').read_text()
    files = [ROOT / 'tools/record_fanaticism_mana_adopted.py', ROOT / 'docs/fanaticism-mana-adopted-checkpoint.md',
        ROOT / 'tools/reference/fanaticism-mana-approval.json', ROOT / approval['proposal'], ROOT / approval['testedCandidateEvidence'],
        ROOT / 'tools/build_skill_roadmap.py', ROOT / 'data/Sanctuary/Balance/class-skill-status.json',
        STAGE / 'source-snapshot-1.json', STAGE / 'birth-inputs.json', STAGE / 'birth-native-checks.log',
        STAGE / 'native-run-1.log', STAGE / 'content-build-1.log', STAGE / 'integration-build-1.log']
    files += list(configuration()['extra_inputs'])
    result = dict(component='Approved mana-neutral Fanaticism self damage', referenceVersion='3.3.93847',
        approved=True, defaultRuleEnabled=True, nativeMarkers=47, markers=dict(sorted(expected_markers().items())),
        actualWireInputs=12, zeroManaFanaticismInputs=6, connections=14, boots=3, saves=3, reloads=2,
        contentBuild=1, integrationBuild=1, sourceSnapshot=1, nativeRun=1, artifacts=born['artifacts'],
        changedSources=sorted(changed), runtimeDataChange='Fanaticism remaining-description only', nativeRebuilt=False,
        persistedSchemaChanged=False, installed=False, classCertified=False, goalComplete=False,
        saveDeletionWarnings=re.findall(r'Warning: Attempted to delete (.+) during world save\.', log),
        unsupportedWarnings=[line for line in log.splitlines() if ' WRN]' in line and 'unsupported' in line.lower()],
        protection=e.protection('tools/reference/slow-missiles-learned-client-evidence.json', 44),
        limits='Successful zero-cost self application only. Rank/creator/whole-packet replacement,AR/rate/allies and cost remain unchanged. Equal/stronger competing sources,full modern stat/event/PvP/client and whole-class parity remain unqualified. Existing effects/audio/icons remain separate placeholders. Four Warlock proposals remain frozen pending;no charms/runewords.')
    e.write('fanaticism-mana-adopted-native-evidence.json', result, files, check)
    print('Approved Fanaticism:47 results/12actual inputs/two reloads;zero-mana personal bonus enabled.')


if __name__ == '__main__':
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--check', action='store_true')
    record(p.parse_args().check)
