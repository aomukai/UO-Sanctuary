#!/usr/bin/env python3
"""Bind the scoped NPC proc producer; do not admit unavailable named items."""
import argparse
from collections import Counter
import sys
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot

ROOT = e.ROOT
STAGE = ROOT / '.local/class-skills/iron-golem-fireball'
sys.path.insert(0, str(ROOT / 'tests'))
from check_iron_golem_fireball import configuration


def record(check=False):
    born = e.load(STAGE / 'birth-inputs.json')
    snapshot(STAGE / 'source-snapshot-5.json', STAGE / 'birth-inputs.json')
    parent = e.load(ROOT / '.local/class-skills/fanaticism-mana/birth-inputs.json')
    changed = {p for p, h in born['sourceInputs'].items() if p.endswith('.cs') and parent['sourceInputs'].get(p) != h}
    assert changed == {'src/Sanctuary.Content/' + n + '.cs' for n in (
        'IronGolemFireBall', 'NpcItemProcGraph', 'NpcItemSkillExecution', 'NpcItemSpellDamage',
        'NpcItemSpellFlights', 'NpcItemSpellOrigins', 'NpcWorldAllocation', 'SorceressSpellMissiles')} | {
        'tests/Sanctuary.Integration/IronGolemFireBallChecks.cs'}
    assert born['copiedData'] == parent['copiedData']
    for p, h in born['sourceInputs'].items():
        assert e.sha(ROOT / p) == h, p
    for p, h in born['copiedData'].items():
        assert e.sha(ROOT / 'data' / p.removeprefix('Data/')) == h, p
    for p in ('ModernUO.dll', 'Server.dll', 'Assemblies/UOContent.dll'):
        assert e.sha(STAGE / 'native/Distribution' / p) == born['artifacts'][p] == parent['artifacts'][p]
    for kind in ('Content', 'Integration'):
        assert e.sha(STAGE / kind.lower() / f'Sanctuary.{kind}.dll') == born['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
        text = (STAGE / (kind.lower() + '-build-5.log')).read_text()
        assert all(v in text for v in ('Build succeeded.', '0 Warning(s)', '0 Error(s)'))
    expected = Counter({'iron-golem-birth-check': 3})
    for family in ('iron-golem-items', 'npc-resource-callbacks', 'iron-golem-fireball'):
        expected[family + '-check'] = 1
        expected[family + '-reload-check'] = 2
    log = e.cohort(STAGE, expected, 12)
    assert 'pass across3boots/2reloads' in (STAGE / 'native-run-4.log').read_text()
    files = [ROOT / 'tools/record_npc_item_fireball.py', ROOT / 'docs/npc-item-fireball-checkpoint.md',
        ROOT / 'tests/check_iron_golem_birth.py', ROOT / 'tests/check_britain.py',
        STAGE / 'source-snapshot-5.json', STAGE / 'birth-inputs.json', STAGE / 'birth-native-checks.log',
        STAGE / 'native-run-4.log', STAGE / 'content-build-5.log', STAGE / 'integration-build-5.log']
    files += list(configuration()['extra_inputs'])
    files += [STAGE / n for n in ('failed-run-1.tar.gz', 'failed-build-2.tar.gz', 'failed-run-2.tar.gz', 'failed-run-3.tar.gz')]
    result = dict(component='Scoped original NPC Fire Ball proc producer', referenceVersion='3.3.93847',
        nativeMarkers=12, markers=dict(sorted(expected.items())), boots=3, saves=3, reloads=2,
        contentBuild=5, integrationBuild=5, sourceSnapshot=5, nativeRun=4, artifacts=born['artifacts'],
        changedSources=sorted(changed), runtimeDataChanged=False, nativeRebuilt=False,
        persistedSchemaChanged=False, installed=False, classCertified=False, goalComplete=False,
        automaticWholeItemGolemAdmission=False, namedAcquisitionChanged=False,
        protection=e.protection('tools/reference/slow-missiles-learned-client-evidence.json', 44),
        limits='Existing explicit event carrier on an actual Iron Golem/plain original. Pinned Hellslayer chance/rank arguments use the existing writer and callback,not a full Hellslayer. Whole-item projection/admission,automatic learned integration,nonzero mastery/pierce,all recipient/terrain/current-executable/graphical parity remain unqualified. NPC flights retain their earlier terrain mapping;player-only terrain approval is not extended. Existing Fire Ball/audio/icon placeholders are reused. No charms/runewords.')
    e.write('npc-item-fireball-native-evidence.json', result, files, check)
    print('NPC Fire Ball producer:12 results/two reloads;whole-item acquisition remains gated.')


if __name__ == '__main__':
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--check', action='store_true')
    record(p.parse_args().check)
