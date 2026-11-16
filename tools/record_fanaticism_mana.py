#!/usr/bin/env python3
"""Record the prepared mana-neutral Fanaticism self packet, not its adoption."""
import argparse
from collections import Counter
import re
import sys

import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot

ROOT = e.ROOT
STAGE = ROOT / '.local/class-skills/fanaticism-mana'
sys.path.insert(0, str(ROOT / 'tests'))
from check_fanaticism_mana import configuration, INPUTS


def expected_markers():
    config = configuration()
    expected = Counter({'iron-golem-birth-check': 3, 'offensive-aura-check': 3})
    for family in ('iron-golem-items', 'npc-resource-callbacks', *config['extra_families']):
        for suffix, count in (('-check', 1), ('-reload-check', 2)):
            command = family + suffix
            expected[command] += count
            if command in config['extra_wait_results']:
                expected[config['extra_wait_results'][command]] += count
    for command in INPUTS:
        expected[command + '-check'] += 3
        expected[command + '-result'] += 3
    assert sum(expected.values()) == 47
    return expected


def record(check=False):
    run = STAGE / 'native-run-1.log'
    assert 'pass across3boots/2reloads' in run.read_text()
    born = e.load(STAGE / 'birth-inputs.json')
    snapshot(STAGE / 'source-snapshot-1.json', STAGE / 'birth-inputs.json')
    parent = e.load(ROOT / '.local/class-skills/unresisted-shields/birth-inputs.json')
    changed = {name for name, digest in born['sourceInputs'].items()
        if name.endswith('.cs') and digest != parent['sourceInputs'].get(name)}
    assert changed == {'src/Sanctuary.Content/PaladinAuras.cs', 'tests/Sanctuary.Integration/FanaticismManaChecks.cs'}
    assert born['copiedData'] == parent['copiedData']
    for name, digest in born['sourceInputs'].items():
        assert e.sha(ROOT / name) == digest, name
    for name, digest in born['copiedData'].items():
        assert e.sha(ROOT / 'data' / name.removeprefix('Data/')) == digest, name
    for name in ('ModernUO.dll', 'Server.dll', 'Assemblies/UOContent.dll'):
        assert e.sha(STAGE / 'native/Distribution' / name) == born['artifacts'][name] == parent['artifacts'][name]
    for kind in ('Content', 'Integration'):
        assert e.sha(STAGE / kind.lower() / f'Sanctuary.{kind}.dll') == born['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
        build = (STAGE / (kind.lower() + '-build-1.log')).read_text()
        assert all(value in build for value in ('Build succeeded.', '0 Warning(s)', '0 Error(s)'))
    source = (ROOT / 'src/Sanctuary.Content/PaladinAuras.cs').read_text()
    assert 'internal static bool PreparedFanaticismManaNeutral;' in source
    assert 'fullSelfDamage||PreparedFanaticismManaNeutral&&values.Mana256==0' in source
    log = e.cohort(STAGE, expected_markers(), 47)
    assert log.count('Client: 127.0.0.1: Connected.') == 14 and 'denied by connection filter' not in log
    files = [ROOT / 'tools/record_fanaticism_mana.py', ROOT / 'docs/fanaticism-mana-checkpoint.md',
        ROOT / 'docs/fanaticism-mana-proposal.md', ROOT / 'tools/reference/fanaticism-mana-proposal.json',
        ROOT / 'src/Sanctuary.Content/PaladinAuras.cs',
        ROOT / 'tests/Sanctuary.Integration/FanaticismManaChecks.cs',
        STAGE / 'birth-inputs.json', STAGE / 'birth-native-checks.log', run,
        STAGE / 'source-snapshot-1.json', STAGE / 'content-build-1.log', STAGE / 'integration-build-1.log']
    files += list(configuration()['extra_inputs'])
    result = dict(component='Prepared mana-neutral Fanaticism self packet', referenceVersion='3.3.93847',
        approvalRequired=True, defaultRuleEnabled=False, nativeMarkers=47,
        markers=dict(sorted(expected_markers().items())), existingWireInputs=12,
        connections=14, boots=3, saves=3, reloads=2, sourceSnapshot=1,
        contentBuild=1, integrationBuild=1, nativeRun=1, artifacts=born['artifacts'],
        changedSources=sorted(changed), runtimeDataChanged=False, nativeRebuilt=False,
        persistedSchemaChanged=False, installed=False, classCertified=False, goalComplete=False,
        saveDeletionWarnings=re.findall(r'Warning: Attempted to delete (.+) during world save\.', log),
        unsupportedWarnings=[line for line in log.splitlines() if ' WRN]' in line and 'unsupported' in line.lower()],
        protection=e.protection('tools/reference/slow-missiles-learned-client-evidence.json', 44),
        limits='Prepared successful self application only: live default retains the older zero-mana rule. Rank/creator/replacement and equal/stronger competing-source interactions remain unchanged and not newly certified. Learned-input qualification under adopted rule follows a decision. No new assets/charms/runewords.')
    e.write('fanaticism-mana-native-evidence.json', result, files, check)
    print('Prepared Fanaticism:47 results/12existing inputs/2reloads;default rule unchanged.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
