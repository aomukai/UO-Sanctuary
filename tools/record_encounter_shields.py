#!/usr/bin/env python3
"""Bind the encounter producer fix to shared shield, save and catalogue checks."""
import argparse
import json
from collections import Counter
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/encounter-shields'
sha = inventory.sha


def record(build, run, audit_run, check=False):
    inventory.STAGE = STAGE
    inputs = {n: inventory.validate_inventory(STAGE / (n + '-inputs.json')) for n in ['birth', 'audit']}
    for name, value in inputs.items():
        snapshot(STAGE / f'pre-build-{build}.json', STAGE / (name + '-inputs.json'))
        assert value['artifacts'] == inputs['birth']['artifacts'] and value['copiedData'] == inputs['birth']['copiedData']
    parent = ROOT / 'tools/reference/summon-resist-native-evidence.json'
    previous = json.loads(parent.read_text())
    ref = previous['inputInventories']['birth']
    assert sha(ROOT / ref['path']) == ref['sha256']
    old = json.loads((ROOT / ref['path']).read_text())
    before = {p: h for p, h in old['sourceInputs'].items() if p.endswith('.cs')}
    after = {p: h for p, h in inputs['birth']['sourceInputs'].items() if p.endswith('.cs')}
    changed = {'src/Sanctuary.Content/' + n + '.cs' for n in ['DefensiveArmors', 'SkillCombat', 'EliteCombat']}
    added = {'tests/Sanctuary.Integration/EncounterShieldChecks.cs'}
    assert set(before) <= set(after) and set(after) - set(before) == added
    assert {p for p in before if before[p] != after[p]} == changed
    assert inputs['birth']['copiedData'] == old['copiedData']
    for name in ['ModernUO.dll', 'Server.dll', 'Assemblies/UOContent.dll']:
        assert sha(STAGE / 'native/Distribution' / name) == inputs['birth']['artifacts'][name] == old['artifacts'][name]
    expected = Counter({'iron-golem-birth-check': 3, 'encounter-shields-check': 3})
    for name in ['iron-golem-items', 'npc-resource-callbacks', 'bone-armor', 'psychic-ward', 'cyclone-armor']:
        expected[name + '-check'] = 1
        expected[name + '-reload-check'] = 2
    for name in ['bone-armor-right', 'bone-armor-quick']:
        expected[name + '-check'] = expected[name + '-result'] = 3
    assert sum(expected.values()) == 33 and markers(STAGE / 'birth-native-checks.log') == expected
    text = (STAGE / 'birth-native-checks.log').read_text()
    assert ': failed' not in text and ' ERR]' not in text
    assert text.count('Shutting down') == text.count('Writing world save snapshot done') == 3
    assert text.count('Encounter shields:learned Bone Armor') == 3
    assert 'pass across3boots/2reloads' in (STAGE / f'run-{run}.log').read_text()
    for kind in ['content', 'integration']:
        text = (STAGE / f'{kind}-build-{build}.log').read_text()
        assert all(s in text for s in ['Build succeeded.', '0 Warning(s)', '0 Error(s)'])
    for name, count in [('named-item-runtime-audit.json', 861), ('first-loop-affix-audit.json', 21)]:
        a = json.loads((STAGE / name).read_text())
        b = json.loads((ROOT / '.local/class-skills/summon-resist' / name).read_text())
        assert a == json.loads((ROOT / 'tools/reference' / name).read_text())
        assert all(a[k] == v for k, v in b.items() if k not in ['ContentAssemblyPath', 'ContentAssemblySha256', 'InputHashes'])
        assert len(a['InputHashes']) == count and a['ContentAssemblySha256'] == inputs['birth']['artifacts']['Assemblies/Sanctuary.Content.dll']
        for path, h in a['InputHashes'].items():
            assert sha(ROOT / path) == h, path
    assert markers(STAGE / 'audit-native-checks.log') == Counter({'paladin-thorns-check': 1, 'paladin-thorns-result': 1, 'named-gear-check': 1, 'first-loop-catalogue-check': 1})
    text = (STAGE / 'audit-native-checks.log').read_text()
    assert ': failed' not in text and ' ERR]' not in text and text.count('Shutting down') == 1
    assert 'Ran 71 tests' in (STAGE / 'python-tests.log').read_text() and '\nOK\n' in (STAGE / 'python-tests.log').read_text()
    assert '573 definitions accounted for' in (STAGE / 'named-readiness.log').read_text()
    assert 'Eight classes, 240 source skills' in (STAGE / 'catalogue-check.log').read_text()
    bone = json.loads((STAGE / 'bone-armor-fixtures.json').read_text())
    assert len(bone) == 2 and all(p['Generation'] == 2 for p in bone)
    assert bone[0]['Remaining256'] == 16637 and bone[1]['Remaining256'] == 5118
    assert len(json.loads((STAGE / 'psychic-ward-fixtures.json').read_text())) == 2
    cyclone = json.loads((STAGE / 'cyclone-armor-fixtures.json').read_text())
    assert cyclone['State']['Rank'] == 2 and cyclone['State']['Remaining256'] == 13823
    failed = (STAGE / 'failed-birth-run-1.log').read_text()
    assert 'Prepared Ward creation' in failed and 'encounter-shields-check: failed' in failed
    installed = json.loads((ROOT / 'tools/reference/creeper-frame-installation-evidence.json').read_text())
    assert len(installed['installedArtifacts']) == 106
    for path, h in installed['installedArtifacts'].items():
        assert sha(Path(installed['runtimeRoot']) / path) == h, path
    status = json.loads((ROOT / 'data/Sanctuary/Balance/class-skill-status.json').read_text())
    handlers = [r for r in status['skills'] if r['implementation'] == 'handler-present']
    assert len(handlers) == 186 and sum(r['characterClass'] == 'Necromancer' for r in handlers) == 30 and not status['completedClasses']
    docs = ['encounter-shields-checkpoint', 'bone-armor-checkpoint', 'class-skill-roadmap', 'class-skills-resume-checkpoint', 'necromancer-mechanical-audit', 'skill-effects', 'skill-icons']
    files = [parent, Path(__file__).resolve(), ROOT / 'tests/check_encounter_shields.py', *[ROOT / p for p in changed | added], *[ROOT / f'docs/{n}.md' for n in docs]]
    files += [STAGE / p for p in [f'pre-build-{build}.json', f'content-build-{build}.log', f'integration-build-{build}.log', f'run-{run}.log', f'audit-run-{audit_run}.log', 'birth-inputs.json', 'audit-inputs.json', 'birth-native-checks.log', 'audit-native-checks.log', 'python-tests.log', 'named-readiness.log', 'catalogue-check.log', 'bone-armor-fixtures.json', 'psychic-ward-fixtures.json', 'cyclone-armor-fixtures.json', 'failed-birth-run-1.log', 'failed-birth-inputs-1.json']]
    result = dict(referenceVersion='3.3.93847', component='Encounter damage shared incoming shields', installed=False, goalComplete=False, completedClasses=[], build=build, run=run, auditRun=audit_run,
                  verified=dict(nativeMarkers=33, boots=3, actualReloads=2, saves=3, actualBoneArmorInputCasts=6, auditMarkers=4, pythonTests=71, namedDefinitions=573, auditFingerprints=[861, 21], unchangedInstalledArtifacts=106, handlers=186, necromancerHandlers=30),
                  inputInventories={n: dict(path=str((STAGE / (n + '-inputs.json')).relative_to(ROOT)), sha256=sha(STAGE / (n + '-inputs.json'))) for n in inputs},
                  changedSourceHashes={p: after[p] for p in sorted(changed | added)}, artifacts={str(p.relative_to(ROOT)): sha(p) for p in sorted(set(files))},
                  established=['The existing class pre-resistance shield sequence is extracted once and reused by class and encounter damage. Encounter damage previously called Cyclone Armor alone and bypassed Bone Armor, prepared Ward and Energy Shield.',
                               'Every boot tests actual learned Bone Armor direct/weapon/mixed/fractional/Shaftstop/rejected-range/unshielded encounter controls; prepared Ward physical-only and exhaustion; existing Energy Shield physical/fire/magic mana and zero-mana clearing. Ward learned casting and automatic stun remain gated.',
                               'Existing three shield component/save fixtures, six connected Bone Armor right/quick casts and original-item/NPC resources pass. Exact pre-build/native/audit inventories match; data/kernel/installed artifacts remain unchanged. No deployment.'],
                  remaining=['Modern Bone Armor breaking-hit behavior, current mixed-shield event priority/registration and complete source/stat/producer/HP/death/PvP/client parity remain unverified. The fixed class sequence is retained, not recertified as a complete modern event registry.',
                             'Encounter damage retains its previous later reducers and whole-HP settlement. Fractional incoming shield consumption is tested; general fractional encounter life settlement remains unfinished.',
                             'The first run caught a Ward fixture that omitted prerequisite synergies. The corrected test preserves its actual captured maximum and explicitly inspects an injured pool. No complete skill/class, old-binary migration or final-asset claim.'])
    path = ROOT / 'tools/reference/encounter-shields-native-evidence.json'
    rendered = json.dumps(result, indent=2) + '\n'
    if check:
        assert path.read_text() == rendered
    else:
        path.write_text(rendered)
    print('Encounter shields:33 native markers/3boots/2reloads/6real casts; shared consumers,4audits/71Python;installed unchanged.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--build', type=int, required=True)
    parser.add_argument('--run', type=int, required=True)
    parser.add_argument('--audit-run', type=int, required=True)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    record(args.build, args.run, args.audit_run, args.check)
