#!/usr/bin/env python3
"""Bind exact bound-item resource acceptance to its source, binaries and saves."""
import argparse
from collections import Counter
import json
from pathlib import Path
import subprocess
import record_grizzly_body as inventory
from record_revive_regeneration import checked_log, markers
from snapshot_skill_build import check as check_snapshot

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/npc-equipment-resources'
PREDECESSOR = '7d163d9'
sha = inventory.sha


def historical(name):
    return json.loads(subprocess.check_output(['git', 'show', PREDECESSOR + ':' + name], cwd=ROOT))


def record(check=False):
    inventory.STAGE = STAGE
    snapshot = STAGE / 'pre-build-attempt-10.json'
    paths = [Path(__file__).resolve(), snapshot]
    scopes = []
    for name in ['resources-inputs.json', 'stats-inputs.json', 'audit-inputs.json', 'upgrade-inputs.json']:
        p = STAGE / name
        scopes.append(inventory.validate_inventory(p))
        check_snapshot(snapshot, p)
        paths.append(p)
    focused, retained, audited, upgrade = scopes
    assert all(x['artifacts'] == focused['artifacts'] and x['copiedData'] == focused['copiedData'] for x in scopes)
    prior = historical('tools/reference/npc-equipment-mitigation-evidence.json')
    old = prior['inputInventory']
    assert old['copiedData'] == focused['copiedData']
    for name in ['ModernUO.dll', 'Server.dll', 'Assemblies/UOContent.dll']:
        assert old['artifacts'][name] == focused['artifacts'][name] == sha(STAGE / 'native/Distribution' / name)
    old_cs = {p: h for p, h in old['sourceInputs'].items() if p.endswith('.cs')}
    new_cs = {p: h for p, h in focused['sourceInputs'].items() if p.endswith('.cs')}
    assert set(new_cs) - set(old_cs) == {
        'src/Sanctuary.Content/NpcEquipmentResources.cs', 'src/Sanctuary.Content/NpcResourceValues.cs',
        'tests/Sanctuary.Integration/NpcEquipmentResourceChecks.cs'}
    assert {p for p in old_cs if old_cs[p] != new_cs[p]} == {
        'src/Sanctuary.Content/BattleOrders.cs', 'src/Sanctuary.Content/ClassGear.cs',
        'src/Sanctuary.Content/GearProfiles.cs', 'src/Sanctuary.Content/IronGolemItems.cs',
        'src/Sanctuary.Content/ItemStatContext.cs', 'src/Sanctuary.Content/NecromancerEffects.cs',
        'src/Sanctuary.Content/PaladinVigor.cs', 'src/Sanctuary.Content/SkillHealth.cs',
        'tests/Sanctuary.Integration/NpcEquipmentResistanceChecks.cs'}
    scope = checked_log(STAGE / 'resources-native-checks.log', 3)
    assert scope == Counter({'npc-equipment-resources-check': 1, 'npc-equipment-resources-reload-check': 2})
    complete = checked_log(STAGE / 'stats-native-checks.log', 392)
    previous = ROOT / '.local/class-skills/npc-equipment-mitigation/stats-native-checks.log'
    assert sha(previous) == prior['artifacts'][str(previous.relative_to(ROOT))]
    assert complete == markers(previous.read_text()) + scope
    source = ROOT / 'tools/reference/npc-equipment-resources-source-evidence.json'
    proof = json.loads(source.read_text())
    assert proof['referenceVersion'] == '3.3.93847' and proof['cases'] == 1152
    assert proof['legacyCommit'] == '0c0bbd1e6bef9181a3da1c046523af622a925416'
    for name, digest in (proof['inputs'] | proof['artifacts']).items():
        assert sha(ROOT / name) == digest, name
        paths.append(ROOT / name)
    cases = ROOT / '.local/class-skills/npc-equipment-resources-source/cases.json'
    assert len(json.loads(cases.read_text())) == 1152
    assert focused['sourceInputs'][str(cases.relative_to(ROOT))] == sha(cases)
    paths.extend([source, ROOT / 'tools/probe_npc_equipment_resources.py'])
    assert checked_log(STAGE / 'upgrade-native-checks.log', 3) == Counter({
        'npc-equipment-resources-legacy-create-check': 1, 'npc-equipment-resources-legacy-upgrade-check': 1,
        'npc-equipment-resources-legacy-reload-check': 1})
    old_content = ROOT / '.local/class-skills/npc-equipment-mitigation/content/Sanctuary.Content.dll'
    assert sha(old_content) == upgrade['oldArtifacts']['Sanctuary.Content.dll'] == old['artifacts']['Assemblies/Sanctuary.Content.dll']
    receipt = subprocess.check_output(['git', 'show', PREDECESSOR + ':tools/reference/npc-equipment-mitigation-evidence.json'], cwd=ROOT)
    import hashlib
    assert upgrade['oldReceipt']['commit'] == PREDECESSOR and upgrade['oldReceipt']['sha256'] == hashlib.sha256(receipt).hexdigest()
    assert len(upgrade['boots']) == 3
    for i, boot in enumerate(upgrade['boots']):
        expected = dict(focused['artifacts'])
        if i == 0:
            expected['Assemblies/Sanctuary.Content.dll'] = sha(old_content)
        assert boot['index'] == i and boot['artifacts'] == expected and boot['savedFiles']
    paths.append(old_content)
    audit_log = (STAGE / 'audit-native-checks.log').read_text()
    assert ': failed' not in audit_log and ' ERR]' not in audit_log and audit_log.count(': passed') == 2 and audit_log.count('Shutting down') == 1
    audits = []
    for name in ['named-item-runtime-audit.json', 'first-loop-affix-audit.json']:
        p = STAGE / name
        a = json.loads(p.read_text())
        audits.append(a)
        assert a == json.loads((ROOT / 'tools/reference' / name).read_text())
        assert a['ContentAssemblySha256'] == focused['artifacts']['Assemblies/Sanctuary.Content.dll']
        for n, h in a['InputHashes'].items():
            assert sha(ROOT / n) == h, n
        paths.extend([p, ROOT / 'tools/reference' / name])
    assert [len(a['InputHashes']) for a in audits] == [796, 21]
    assert len(audits[0]['Definitions']) == 573 and audits[0]['Definitions'] == historical('tools/reference/named-item-runtime-audit.json')['Definitions']
    old_affixes = historical('tools/reference/first-loop-affix-audit.json')
    for field in ['ImplementedCodes', 'SpawnableCodes', 'Items', 'Pools']:
        assert audits[1][field] == old_affixes[field], field
    python = STAGE / 'python-tests.log'
    assert 'Ran 71 tests' in python.read_text() and '\nOK\n' in python.read_text()
    paths.append(python)
    installed = historical('tools/reference/creeper-frame-installation-evidence.json')
    assert len(installed['installedArtifacts']) == 106
    for n, h in installed['installedArtifacts'].items():
        assert sha(Path(installed['runtimeRoot']) / n) == h, n
    for kind in ['content', 'integration']:
        p = STAGE / (kind + '-build-attempt-10.log')
        assert all(x in p.read_text() for x in ['Build succeeded.', '0 Warning(s)', '0 Error(s)'])
        paths.extend([p, STAGE / kind / ('Sanctuary.' + kind.title() + '.dll')])
    for name in ['resources-native-checks.log', 'stats-native-checks.log', 'audit-native-checks.log', 'upgrade-native-checks.log',
                 'isolated-npc-equipment-resources-fixtures.json', 'npc-equipment-resources-fixtures.json',
                 'upgrade-npc-equipment-resources-legacy-fixtures.json', 'run-attempt-10.log', 'retained-run-attempt-1.log', 'upgrade-run-attempt-1.log']:
        paths.append(STAGE / name)
    for name in ['isolated-npc-equipment-resources-fixtures.json', 'npc-equipment-resources-fixtures.json']:
        saved = json.loads((STAGE / name).read_text())
        assert saved['Generation'] == 2 and len(saved['Actors']) == 7
        rows = saved['Actors']
        assert [x['Life'] for x in rows] == [11904, 8064, 20864, 8064, 23104, 8064, 8064]
        assert [x['Mana'] for x in rows] == [0, 0, 8960, 0, 0, 24064, 0]
    legacy = json.loads((STAGE / 'upgrade-npc-equipment-resources-legacy-fixtures.json').read_text())
    assert legacy['Generation'] == 2 and legacy['LifeMaximum'] == 31 and legacy['ManaMaximum'] == 1
    for n in ['tests/check_npc_equipment_resources.py', 'tests/check_npc_equipment_resources_regression.py',
              'tests/check_npc_equipment_resources_upgrade.py', 'docs/skill-effects.md', 'docs/skill-icons.md']:
        paths.append(ROOT / n)
    status = json.loads((ROOT / 'data/Sanctuary/Balance/class-skill-status.json').read_text())
    assert len([x for x in status['skills'] if x['implementation'] == 'handler-present']) == 185 and status['completedClasses'] == []
    result = dict(referenceVersion='3.3.93847', component='Original bound NPC equipment exact resource maxima and fractional balances', installed=False,
        goalComplete=False, completedClasses=[], verified=dict(olderSourceContexts=1152, focusedMarkers=3, retainedMarkers=392,
        legacyUpgradeMarkers=3, separateAuditMarkers=2, bootsPerNativeScope=3, actualReloadsPerNativeScope=2,
        pythonTests=71, inputFingerprints=[796, 21], unchangedNamedDefinitions=573, unchangedInstalledArtifacts=106),
        inputInventory=retained, focusedInputInventory=focused, upgradeInputInventory=upgrade,
        artifacts={str(p.relative_to(ROOT)): sha(p) for p in sorted(set(paths))},
        established=[
            'Shared full item stat graph supplies exact FP256 life/mana/stamina maxima, source per-level shifts and additive percentage pools. NPCs do not gain player resource coefficients from item vitality/energy. Native fields display ceilings; balances retain fractions.',
            'Seven actual originals verify flat life/mana, source-disabled Blacktongue negative HP exclusion, per-level fractions, zero-base mana percentages, explicit zero seed, broken/unidentified/removal, BO/Vigor and facet relative life. Attachment changes preserve absolute balances within the new caps; no free heal/refill.',
            'An accepted old-content boot saves a Crown of Thieves original with old 31 life/1 mana maxima. Two candidate boots retain identity/full rolls and exact saved mana while supplying new item maxima. Existing life/stamina recovery remains active; no new serialization fields.',
            'Integration corrigendum: previous resistance fixture used a Necromancer to supply Conviction, giving rank zero. This suite uses a real Paladin and asserts nonzero aura strength before testing item-created immunity. Resource BO fixtures similarly use a real Barbarian. Production aura mechanics were unchanged.'],
        remaining=[
            'Older full-routine source controls are not a complete modern executable or item-birth certificate. Negative flat live suppliers and positive raw stamina per-level item birth are not certified by the chosen actual originals.',
            'Complete NPC regeneration/event/proc consumers, poison-length/freeze properties, independently frozen Revive equipment and learned Iron Golem remain unfinished. Existing innate Fire Golem absorption is not certified.',
            'Revive inherits living abilities; global UO monster spell conversion remains a separate task. No new handler/class completion, full class suite or deployment is claimed. Effects/audio and talent/hotkey icons remain separately recorded placeholders.'])
    target = ROOT / 'tools/reference/npc-equipment-resources-evidence.json'
    rendered = json.dumps(result, indent=2) + '\n'
    if check:
        assert target.read_text() == rendered
    else:
        target.write_text(rendered)
    print('NPC resources acceptance:1152source/3focused/392retained/3legacy-upgrade/2audits/71Python;remaining scope explicit.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
