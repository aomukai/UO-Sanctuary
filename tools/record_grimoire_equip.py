#!/usr/bin/env python3
"""Bind original Grimoire stats,atomic pairing and actual old-build upgrades."""
import argparse
import hashlib
import json
import struct
import subprocess
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from review_weapon_event_order import function
from snapshot_skill_build import check as snapshot

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/grimoire-equip'
FOCUS = ROOT / '.local/class-skills/grimoire-equip-focus'
PARENT = '4b0e3e09a20f37525f596108cf26ee85cc9165a6'
sha = inventory.sha


def record(check=False):
    inventory.STAGE = STAGE
    native = inventory.validate_inventory(STAGE / 'birth-inputs.json')
    focused = inventory.validate_inventory(FOCUS / 'birth-inputs.json')
    audit = inventory.validate_inventory(STAGE / 'audit-inputs.json')
    upgrade = inventory.validate_inventory(STAGE / 'upgrade-inputs.json')
    for folder, name in ((STAGE, 'birth-inputs.json'), (FOCUS, 'birth-inputs.json'), (STAGE, 'audit-inputs.json'), (STAGE, 'upgrade-inputs.json')):
        snapshot(STAGE / 'pre-build-1.json', folder / name)
    assert native['artifacts'] == focused['artifacts'] == audit['artifacts'] == upgrade['artifacts']
    assert native['copiedData'] == focused['copiedData'] == audit['copiedData'] == upgrade['copiedData']
    previous = ROOT / '.local/class-skills/grimoire-capture'
    prior = json.loads((previous / 'birth-inputs.json').read_text())
    changed = {name for name in set(prior['sourceInputs']) | set(native['sourceInputs'])
               if name.endswith('.cs') and prior['sourceInputs'].get(name) != native['sourceInputs'].get(name)}
    assert changed == {'src/Sanctuary.Content/' + name for name in (
        'ClassGear.cs', 'DualEquip.cs', 'EquipmentBalance.cs', 'EquipmentCrafting.cs', 'EquipmentPropertyWriter.cs',
        'FrozenEquipmentEffects.cs', 'GearProfiles.cs', 'GrimoirePrepared.cs', 'ItemStatContext.cs', 'NamedGear.cs',
        'WarlockGrimoire.cs', 'WeaponHands.cs', 'EquipmentIntrinsicProperties.cs', 'GrimoireEquipment.cs', 'WarlockHands.cs')} | {
        'tests/Sanctuary.Integration/DualEquipChecks.cs', 'tests/Sanctuary.Integration/GrimoireCaptureChecks.cs',
        'tests/Sanctuary.Integration/GrimoireEquipChecks.cs'}
    assert native['copiedData'] == prior['copiedData']
    for name in ('ModernUO.dll', 'Server.dll', 'Assemblies/UOContent.dll'):
        assert native['artifacts'][name] == prior['artifacts'][name] == sha(STAGE / 'native/Distribution' / name)
    parentraw = subprocess.check_output(['git', 'show', PARENT + ':tools/reference/grimoire-capture-native-evidence.json'], cwd=ROOT)
    parent = json.loads(parentraw)
    baseline = previous / 'birth-native-checks.log'
    assert sha(baseline) == parent['evidence'][str(baseline.relative_to(ROOT))]
    for folder, old, count in ((STAGE, 'grimoire-capture', 405), (FOCUS, 'grimoire-capture-focus', 73)):
        expected = markers(ROOT / '.local/class-skills' / old / 'birth-native-checks.log')
        for family in ('grimoire-equip', 'levitation'):
            expected[family + '-check'] += 1
            expected[family + '-reload-check'] += 2
        expected['dual-equip-regression-check'] += 3
        assert markers(folder / 'birth-native-checks.log') == expected and sum(expected.values()) == count
        log = (folder / 'birth-native-checks.log').read_text()
        assert ': failed' not in log and ' ERR]' not in log
        assert log.count('Shutting down') == log.count('Writing world save snapshot done') == 3
        saved = json.loads((folder / 'grimoire-equip-fixtures.json').read_text())
        assert len(saved) == 2 and {r['Female'] for r in saved} == {False, True}
        for row in saved:
            assert len({row[n] for n in ('Owner', 'Weapon', 'Book')}) == 3
            assert (row['Mana'], row['Life'], row['DamageMin'], row['DamageMax'], row['Durability']) == (42752, 64256, 13, 30, 17)
            capture = json.loads(row['Capture']); gear = json.loads(row['Gear'])
            assert capture == dict(Code='wa1', ItemLevel=99, Defense=5, Automatic='of Shadows', Grants=[], Profile='captured-normal-grimoire-v1')
            assert gear['BaseCode'] == 'wa1' and gear['Defense'] == 5 and gear['Roll']['ItemLevel'] == 99 and gear['Roll']['RequiredLevel'] == 3
    assert upgrade['oldReceipt'] == dict(commit=PARENT, sha256=hashlib.sha256(parentraw).hexdigest())
    assert len(upgrade['boots']) == 3
    for index, boot in enumerate(upgrade['boots']):
        assert boot['index'] == index and boot['savedFiles'] and all(len(digest) == 64 for digest in boot['savedFiles'].values())
        for name, digest in native['artifacts'].items():
            if index == 0 and name.startswith('Assemblies/Sanctuary.'):
                assert boot['artifacts'][name] == upgrade['oldArtifacts'][name] == parent['tested']['artifacts'][name]
            else:
                assert boot['artifacts'][name] == digest
    for kind in ('content', 'integration'):
        name = 'Assemblies/Sanctuary.' + kind.title() + '.dll'
        assert sha(previous / kind / Path(name).name) == upgrade['oldArtifacts'][name]
        assert all(s in (STAGE / (kind + '-build-1.log')).read_text() for s in ('Build succeeded.', '0 Warning(s)', '0 Error(s)'))
    log = (STAGE / 'upgrade-native-checks.log').read_text()
    assert markers(STAGE / 'upgrade-native-checks.log') == {'grimoire-capture-check': 1, 'grimoire-capture-upgrade-check': 1, 'grimoire-capture-upgrade-reload-check': 1}
    assert ': failed' not in log and ' ERR]' not in log and log.count('Shutting down') == log.count('Writing world save snapshot done') == 3
    legacy = json.loads((STAGE / 'upgrade-grimoire-capture-fixtures.json').read_text())
    assert len(legacy) == 2 and {r['Female'] for r in legacy} == {True, False}
    for row in legacy:
        assert row['Mana'] == 22784 and row['Life'] == 37632 and row['Durability'] == 17
        capture = json.loads(row['Capture']); assert capture['Code'] == ('waf' if row['Female'] else 'wa1')
        assert capture['Defense'] == (148 if row['Female'] else 5) and capture['Grants'] == [dict(Skill=383, Rank=3), dict(Skill=374, Rank=2)]
    # Reuse the immutable owned hand metadata without rerunning its historical
    # unchanged-production guard against this intentionally changed source.
    handpath = ROOT / 'tools/reference/warlock-hands-reference-evidence.json'
    hand = json.loads(handpath.read_text())
    for name, digest in hand['inputs'].items():
        if not name.startswith('src/'):
            assert sha(ROOT / name) == digest, name
    data = (ROOT / '.local/class-skills/warlock-hands-reference/charstats.bin').read_bytes()
    assert len(data) == 1700 and struct.unpack_from('<H', data, 4 + 7 * 212 + 208)[0] == 106 and data[4 + 7 * 212 + 210] == 0
    assert data[4 + 7 * 212 + 69] == 25
    treepath = ROOT / '.local/class-skills/d2moo-source-tree.json'; tree = json.loads(treepath.read_text())
    relative = 'source/D2Common/src/Items/Items.cpp'; sourcepath = ROOT / '.local/class-skills/d2moo-pinned-source' / relative
    raw = sourcepath.read_bytes()
    assert tree['sha'] == '0c0bbd1e6bef9181a3da1c046523af622a925416'
    assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(r['sha'] for r in tree['tree'] if r['path'] == relative)
    required = function(raw.decode(), 'ITEMS_GetRequiredLevel')
    assert 'case ITEMQUAL_NORMAL:' not in required and 'STAT_ITEM_SINGLESKILL' in required and 'pSkillsTxtRecord->wReqLevel > nRequiredLevel' in required
    for name, count in (('named-item-runtime-audit.json', 990), ('first-loop-affix-audit.json', 21)):
        new = json.loads((STAGE / name).read_text())
        assert new == json.loads((ROOT / 'tools/reference' / name).read_text()) and len(new['InputHashes']) == count
        assert new['ContentAssemblySha256'] == native['artifacts']['Assemblies/Sanctuary.Content.dll']
        for filename, digest in new['InputHashes'].items(): assert sha(ROOT / filename) == digest, filename
        old = json.loads(subprocess.check_output(['git', 'show', PARENT + ':tools/reference/' + name], cwd=ROOT))
        skip = {'InputHashes', 'ContentAssemblySha256', 'ContentAssemblyPath'}
        assert {k: v for k, v in new.items() if k not in skip} == {k: v for k, v in old.items() if k not in skip}
    assert (STAGE / 'audit-native-checks.log').read_text().count(': passed') == 2
    assert 'Ran 71 tests' in (STAGE / 'python-tests.log').read_text() and '\nOK\n' in (STAGE / 'python-tests.log').read_text()
    for name, text in (('source-check.log', '15bases/five families/eight automatic rows'), ('readiness-check.log', '573 definitions accounted for'),
                       ('catalogue-check.log', 'Eight classes, 240 source skills'), ('roadmap-check.log', 'Class handler coverage, blockers, effects and icons recorded separately.')):
        assert text in (STAGE / name).read_text()
    installed = json.loads((ROOT / 'tools/reference/creeper-frame-installation-evidence.json').read_text())
    assert len(installed['installedArtifacts']) == 106
    for name, digest in installed['installedArtifacts'].items(): assert sha(Path(installed['runtimeRoot']) / name) == digest, name
    protected = json.loads((ROOT / '.local/class-skills/creeper-frame-installation.json').read_text())['protected']
    for key, relative, count in (('saves', '.local/server/Saves', 38), ('configuration', '.local/server/Configuration', 20)):
        folder = Path(installed['runtimeRoot']) / relative
        assert len(protected[key]) == count and {str(p.relative_to(folder)): sha(p) for p in folder.rglob('*') if p.is_file()} == protected[key]
    state = json.loads((ROOT / 'data/Sanctuary/Balance/class-skill-status.json').read_text())
    assert not state['completedClasses'] and sum(row['implementation'] == 'handler-present' for row in state['skills']) == 191
    files = [Path(__file__).resolve(), handpath, treepath, sourcepath,
             *(ROOT / 'tests' / name for name in ('check_grimoire_equip.py', 'check_grimoire_equip_focus.py', 'check_grimoire_equip_upgrade.py')),
             *(ROOT / 'docs' / name for name in ('grimoire-equip-checkpoint.md', 'grimoire-capture-checkpoint.md', 'class-skill-roadmap.md', 'class-skills-resume-checkpoint.md', 'class-skill-reuse-notes.md')),
             *(STAGE / name for name in ('pre-build-1.json', 'birth-inputs.json', 'audit-inputs.json', 'birth-native-checks.log', 'native-run-1.log',
                                        'content-build-1.log', 'integration-build-1.log', 'audit-native-checks.log', 'audit-run-1.log', 'python-tests.log',
                                        'source-check.log', 'readiness-check.log', 'catalogue-check.log', 'roadmap-check.log', 'grimoire-equip-fixtures.json',
                                        'upgrade-inputs.json', 'upgrade-native-checks.log', 'upgrade-run-1.log', 'upgrade-grimoire-capture-fixtures.json')),
             *(FOCUS / name for name in ('birth-inputs.json', 'birth-native-checks.log', 'native-run-1.log', 'grimoire-equip-fixtures.json'))]
    result = dict(referenceVersion='3.3.93847', parent=PARENT, component='Captured Grimoire gear and innate Warlock hand pairing',
                  handlerEnabled=False, goalComplete=False, completedClasses=[],
                  sourceReview=dict(requiredLevelFunction='ITEMS_GetRequiredLevel', completeBodySha256=hashlib.sha256(required.encode()).hexdigest(),
                                    sourceCommit=tree['sha'], scope='Static complete older normal-quality requirement path;not current execution'),
                  verified=dict(nativeMarkers=405, retainedNativeMarkers=396, focusedNativeMarkers=73, retainedFocusedMarkers=64,
                                boots=3, reloads=2, saves=3, upgradeMarkers=3, upgradeBoots=3, upgradeReloads=2, oldContentAndIntegration=True,
                                originalEquippedPairs=2, originalUpgradedBooks=2, pythonTests=71, namedDecisions=573, auditFingerprints=[990, 21], handlers=191,
                                protectedInstalledArtifactsUnchanged=106, protectedSavesUnchanged=38, protectedConfigurationsUnchanged=20),
                  tested=native, upgrade=upgrade, limitations=[
                      'Captured normal-quality originals only;no random generation,named factory or player acquisition is enabled. Empty unprepared books remain unequippable.',
                      'Owned hand/type/block metadata plus native hybrid-sword,zero-Mastery,both-sex/order,rollback,skill/element/copy/reload tests do not establish complete current executable selectors,all weapon-family exceptions,attack animation or graphical/client drag-equip parity.',
                      'Normal required level follows the reviewed complete older base/direct-skill path and existing project tier floors. Current-build required-level parity and other quality/generation branches remain explicit.',
                      'Actual accepted old content/integration creates and saves original books;new content/integration binds and resaves them twice without recapture or resource/durability/identity reset.',
                      'No installed build/save write,new handler or completed class. Stock book art is a named gear placeholder;skill effects/audio and talent/hotkey icons remain separate.',
                  ], evidence={str(p.relative_to(ROOT)): sha(p) for p in files})
    target = ROOT / 'tools/reference/grimoire-equip-native-evidence.json'
    encoded = json.dumps(result, indent=2) + '\n'
    if check: assert target.read_text() == encoded
    else: target.write_text(encoded)
    print('Grimoire equip:405 retained/73 focused checks;actual old content+integration upgrade3boots/2reloads;original pairs and protected installation/saves preserved;generation remains gated.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(); parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
