#!/usr/bin/env python3
"""Bind saved cold parameters, backward reads and learned whole-item admission."""
import argparse
from collections import Counter
import hashlib
import re
import subprocess
import sys
import tarfile
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot

ROOT = e.ROOT
STAGE = ROOT / '.local/class-skills/cold-parameter'
sys.path.insert(0, str(ROOT / 'tests'))
from check_cold_parameter import configuration, INPUTS


def expected_markers():
    result = Counter({'iron-golem-birth-check': 3})
    for family in ('iron-golem-items', 'npc-resource-callbacks', *configuration()['extra_families']):
        result[family + '-check'] += 1
        result[family + '-reload-check'] += 2
    for command in INPUTS:
        result[command + '-check'] += 3
        result[command + '-result'] += 3
    assert sum(result.values()) == 33
    return result


def record(check=False):
    born = e.load(STAGE / 'birth-inputs.json')
    snapshot(STAGE / 'source-snapshot-4.json', STAGE / 'birth-inputs.json')
    prior = e.load(ROOT / '.local/class-skills/fanaticism-mana-adopted/birth-inputs.json')
    changed = {p for p, h in born['sourceInputs'].items() if p.endswith('.cs') and prior['sourceInputs'].get(p) != h}
    assert changed == {'src/Sanctuary.Content/' + p + '.cs' for p in (
        'EquipmentPropertyWriter', 'GearProfiles', 'ItemStatContext', 'NamedGear', 'NamedPropertyRuntime')} | {
        'tests/Sanctuary.Integration/' + p + 'Checks.cs' for p in (
            'ColdParameter', 'IronGolemBody', 'IronGolemCast', 'IronGolemOrderedContact',
            'ItemState', 'NpcEquipmentElement', 'NpcItemBlind')}
    assert born['copiedData'] == prior['copiedData']
    for p, h in born['sourceInputs'].items():
        assert e.sha(ROOT / p) == h, p
    for p, h in born['copiedData'].items():
        assert e.sha(ROOT / 'data' / p.removeprefix('Data/')) == h, p
    for p in ('ModernUO.dll', 'Server.dll', 'Assemblies/UOContent.dll'):
        assert e.sha(STAGE / 'native/Distribution' / p) == born['artifacts'][p] == prior['artifacts'][p]
    for kind in ('Content', 'Integration'):
        assert e.sha(STAGE / kind.lower() / f'Sanctuary.{kind}.dll') == born['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
        build = (STAGE / (kind.lower() + '-build-4.log')).read_text()
        assert all(v in build for v in ('Build succeeded.', '0 Warning(s)', '0 Error(s)'))
    shared = {'tests/check_iron_golem_birth.py', 'tests/check_britain.py', 'tests/check_assassin_planned_acceptance.py'}
    with tarfile.open(STAGE / 'failed-run-1.tar.gz', 'r|gz') as archive:
        seen = set()
        for member in archive:
            if member.name in shared:
                assert hashlib.sha256(archive.extractfile(member).read()).hexdigest() == e.sha(ROOT / member.name)
                seen.add(member.name)
        assert seen == shared
    legacy = subprocess.check_output(['git', 'show', 'a7d77a28:src/Sanctuary.Content/GearProfiles.cs'], cwd=ROOT)
    assert hashlib.sha256(legacy).hexdigest() == prior['sourceInputs']['src/Sanctuary.Content/GearProfiles.cs']
    log = e.cohort(STAGE, expected_markers(), 33)
    assert 'pass across3boots/2reloads' in (STAGE / 'native-run-4.log').read_text()
    assert log.count('Client: 127.0.0.1: Connected.') == 6 and 'denied by connection filter' not in log
    fixture = (ROOT / 'tests/Sanctuary.Integration/ColdParameterChecks.cs').read_text()
    assert 'Boundaries();LegacyBinary();' in fixture
    files = [ROOT / 'tools/record_cold_parameter.py', ROOT / 'docs/cold-parameter-checkpoint.md',
        STAGE / 'source-snapshot-4.json', STAGE / 'birth-inputs.json', STAGE / 'birth-native-checks.log',
        STAGE / 'native-run-4.log', STAGE / 'content-build-4.log', STAGE / 'integration-build-4.log']
    files += [STAGE / f'failed-run-{n}.tar.gz' for n in range(1, 4)]
    files += [ROOT / p for p in shared] + list(configuration()['extra_inputs'])
    result = dict(component='Saved independent cold parameters and learned whole-original Iron Golem',
        referenceVersion='3.3.93847', nativeMarkers=33, markers=dict(sorted(expected_markers().items())),
        actualWireInputs=6, connections=6, boots=3, saves=3, reloads=2,
        nativeLegacyFixtures=['version0', 'version1', 'version6-to-version7'], legacyV6UpgradeChecks=3,
        equipmentWriteVersion=7, equipmentReadVersions=list(range(8)),
        contentBuild=4, integrationBuild=4, sourceSnapshot=4, nativeRun=4,
        artifacts=born['artifacts'], changedSources=sorted(changed), runtimeDataChanged=False,
        nativeRebuilt=False, installed=False, classCertified=False, goalComplete=False,
        persistedSchemaChanged=True, legacyMissingRollReconstructed=False, namedAcquisitionPolicyChanged=False,
        saveDeletionWarnings=re.findall(r'Warning: Attempted to delete (.+) during world save\.', log),
        unsupportedWarnings=[line for line in log.splitlines() if ' WRN]' in line and 'unsupported' in line.lower()],
        protection=e.protection('tools/reference/slow-missiles-learned-client-evidence.json', 44),
        limits='Current tables plus existing supplementary property17 kernel;exact modern allocation/RNG chronology and every item/recipient/client context remain unverified. New Baranar originals qualify;old missing durations stay absent and whole-item NPC admission refuses. Legacy player items retain prior behavior. Reaper proc/acquisition and whole-item proc admission remain separate. New equipment writes require the updated content reader;installed saves are unchanged. Four Warlock proposals remain frozen;no charms/runewords/new assets.')
    e.write('cold-parameter-native-evidence.json', result, files, check)
    print('Cold parameters:33 results/6whole-original casts/two reloads;v0/v1/v6 compatibility and v7 retention pass.')


if __name__ == '__main__':
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--check', action='store_true')
    record(p.parse_args().check)
