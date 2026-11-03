#!/usr/bin/env python3
"""Verify owned Warlock hand-rule metadata without claiming runtime parity."""
import argparse
import csv
import hashlib
import json
import struct
import subprocess
from pathlib import Path
from snapshot_skill_build import check as snapshot

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/warlock-hands-reference'
PRODUCTION = '524328631d6b8eae7e2e040660bf0dfc34ffa11e'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def probe(check=False):
    inputs = {}

    def pin(path):
        inputs[str(path.relative_to(ROOT))] = sha(path)
        return path.read_bytes()

    extraction = json.loads(pin(STAGE / 'extraction.json'))
    installation = json.loads(pin(ROOT / 'tools/reference/d2r-local-installation-evidence.json'))
    assert extraction['referenceVersion'] == installation['referenceVersion'] == '3.3.93847'
    assert extraction['executableSha256'] == installation['executable']['sha256']
    assert extraction['buildInfoSha256'] == installation['buildInfo']['sha256']
    assert not extraction['processMemoryRead'] and not extraction['gameFilesWritten']
    game = Path(installation['installationPath'])
    assert sha(game / 'D2R.exe') == extraction['executableSha256']
    assert sha(game / '.build.info') == extraction['buildInfoSha256']
    reader = ROOT / '.local/class-skills/defiler-compiled-formula/casc-reference-reader'
    assert sha(reader) == extraction['readerSha256']
    pin(reader)
    for name, meta in extraction['files'].items():
        path = STAGE / name
        assert path.stat().st_size == meta['bytes'] and sha(path) == meta['sha256']
        pin(path)
        pin(STAGE / (name + '.extract.log'))
    tables = {name: list(csv.DictReader((STAGE / (name + '.txt')).read_text().splitlines(), delimiter='\t'))
              for name in ('charstats', 'itemtypes')}
    # Expansion is a loader marker, not a record. Empty ordinary item-type
    # rows ARE records; dropping them produces the wrong Grimoire index.
    classes = [row for row in tables['charstats'] if row['class'] != 'Expansion']
    types = [row for row in tables['itemtypes'] if row['ItemType'] != 'Expansion']
    charbin, typebin = (STAGE / 'charstats.bin').read_bytes(), (STAGE / 'itemtypes.bin').read_bytes()
    assert struct.unpack_from('<I', charbin)[0] == len(classes) == 8 and len(charbin) == 4 + 8 * 212
    assert struct.unpack_from('<I', typebin)[0] == len(types) == 109 and len(typebin) == 4 + 109 * 232
    indices = {row['Code']: i for i, row in enumerate(types) if row['Code']}
    for i, row in enumerate(types):
        base = 4 + i * 232
        assert typebin[base:base + 4] == row['Code'].ljust(4, ' ').encode()
        for slot in (1, 2):
            value = struct.unpack_from('<H', typebin, base + 2 + slot * 2)[0]
            assert value == indices.get(row['Equiv' + str(slot)], 0), (i, slot, value)
    assert indices['grim'] == 106
    assert (types[106]['Equiv1'], types[106]['Equiv2'], types[106]['Class'], types[106]['StaffMods']) == ('shld', 'warl', 'war', 'war')
    char_fields = {'str': 48, 'dex': 49, 'int': 50, 'vit': 51, 'stamina': 52, 'hpadd': 53,
                   'WalkVelocity': 60, 'RunVelocity': 61, 'RunDrain': 62, 'LifePerLevel': 63,
                   'StaminaPerLevel': 64, 'ManaPerLevel': 65, 'LifePerVitality': 66, 'StaminaPerVitality': 67,
                   'ManaPerMagic': 68, 'BlockFactor': 69}
    profiles = []
    for i, row in enumerate(classes):
        base = 4 + i * 212
        assert charbin[base + 32:base + 48].split(b'\0', 1)[0].decode() == row['class']
        for key, offset in char_fields.items():
            assert charbin[base + offset] == int(row[key] or 0), (i, key)
        restriction = struct.unpack_from('<H', charbin, base + 208)[0]
        onehand = charbin[base + 210]
        assert restriction == indices.get(row['TwoHandedOffHandRestrictItemType'], 0)
        assert onehand == int(row['TwoHandedDamageAsOneHanded'] or 0)
        profiles.append(dict(characterClass=row['class'], offhandTypeIndex=restriction,
                             offhandType=row['TwoHandedOffHandRestrictItemType'] or None,
                             twoHandedDamageAsOneHanded=bool(onehand)))
    assert profiles[-1] == dict(characterClass='Warlock', offhandTypeIndex=106, offhandType='grim', twoHandedDamageAsOneHanded=False)
    manifest = json.loads(pin(ROOT / 'tools/reference/d2r-named-equipment/sources.json'))
    for table, rows, name in (('charstats', classes, 'class'), ('itemtypes', [types[106]], 'Code')):
        path = ROOT / 'tools/reference/d2r-named-equipment/json' / (table + '.json')
        assert sha(path) == manifest['files']['json/' + table + '.json']['sha256']
        source = json.loads(pin(path))
        for row in rows:
            named = next(r for r in source.values() if r.get(name) == row[name])
            if table == 'charstats':
                assert named.get('TwoHandedOffHandRestrictItemType', '') == row['TwoHandedOffHandRestrictItemType']
                assert named.get('TwoHandedDamageAsOneHanded', 0) == int(row['TwoHandedDamageAsOneHanded'] or 0)
            else:
                assert all(named.get(k, '') == row[k] for k in ('Equiv1', 'Equiv2', 'Class', 'StaffMods'))
    armor_path = ROOT / 'tools/reference/d2r-named-equipment/json/armor.json'
    assert sha(armor_path) == manifest['files']['json/armor.json']['sha256']
    grimoires = [row for row in json.loads(pin(armor_path)).values() if row.get('type') == 'grim']
    assert len(grimoires) == 15 and len({row['normcode'] for row in grimoires}) == 5
    assert all(row['auto prefix'] == 306 for row in grimoires)
    named = json.loads(pin(ROOT / 'data/Sanctuary/Balance/named-item-bases.json'))
    named_grimoires = [row['code'] for row in named if row.get('itemType') == 'grim']
    assert len(named_grimoires) == 5 and not any(code in {row['normcode'] for row in grimoires} for code in named_grimoires)
    # These are implementation gaps, not an excuse to allow ordinary shields.
    for name, anchor in (
        ('EquipmentBalance.cs', 'definition.RequiredClass == "war"'),
        ('NamedGear.cs', 'Warlock class is not implemented'),
        ('ClassGear.cs', 'This weapon requires both hands.'),
        ('WarlockLevitation.cs', "innate two-hand/offhand rule"),
    ):
        assert anchor in pin(ROOT / 'src/Sanctuary.Content' / name).decode()
    snapshot(ROOT / '.local/class-skills/defiler-body/pre-build-1.json')
    previous = json.loads(subprocess.check_output(['git', 'show', PRODUCTION + ':tools/reference/defiler-body-native-evidence.json'], cwd=ROOT))
    for name, digest in previous['tested']['artifacts'].items():
        relative = ('native/Distribution/' + name if name in ('ModernUO.dll', 'Server.dll', 'Assemblies/UOContent.dll')
                    else 'content/Sanctuary.Content.dll' if name == 'Assemblies/Sanctuary.Content.dll'
                    else 'integration/Sanctuary.Integration.dll')
        assert sha(ROOT / '.local/class-skills/defiler-body' / relative) == digest
    installed = json.loads((ROOT / 'tools/reference/creeper-frame-installation-evidence.json').read_text())
    for name, digest in installed['installedArtifacts'].items():
        assert sha(Path(installed['runtimeRoot']) / name) == digest
    protected = json.loads((ROOT / '.local/class-skills/creeper-frame-installation.json').read_text())['protected']
    for key, relative in (('saves', '.local/server/Saves'), ('configuration', '.local/server/Configuration')):
        path = Path(installed['runtimeRoot']) / relative
        assert {str(p.relative_to(path)): sha(p) for p in path.rglob('*') if p.is_file()} == protected[key]
    pin(Path(__file__).resolve())
    result = dict(referenceVersion='3.3.93847', component='Owned Warlock innate hand-rule metadata',
                  classRows=8, characterStatMatches=8 * len(char_fields), characterRuleMatches=16,
                  itemTypeRows=109, itemTypeCodeMatches=109, itemTypeParentMatches=218,
                  profiles=profiles, grimoireIndex=106, grimoireParents=['shld', 'warl'],
                  cachedGrimoireBases=[row['code'] for row in grimoires], cachedNormalGrimoireFamilies=5,
                  cachedAutomaticPrefixGroup=306, namedCatalogueGrimoireSubset=named_grimoires,
                  productionCheckpoint=PRODUCTION, productionChanged=False, handlerEnabled=False,
                  currentExecutableBehaviorVerified=False,
                  protectedInstalledArtifacts=len(installed['installedArtifacts']), protectedSaves=len(protected['saves']),
                  protectedConfigurations=len(protected['configuration']), inputs=inputs,
                  next=['Prepare all15Grimoire bases from the full pinned armor table,not the5named-only subset;source auto-prefix group306 and Warlock staffmods before ordinary factory rollout.',
                        'Implement Grimoire runtime bases/appearance/type ancestry and ordinary factory without enabling unsupported named effects.',
                        'Extend the existing atomic equip transaction for innate Warlock two-handed weapon plus Grimoire in both equip orders;retain real two-handed damage and reject ordinary shields.',
                        'Verify both sexes,zero mastery,equipment failures/rollback,hand removal and actual saved original items;keep animation/casting/current-runtime boundaries explicit.'],
                  limitations=['Metadata is independently corroborated by owned TXT and BIN. It is not execution of the current equip or damage selector.',
                               'Normal Grimoire bases are excluded from the runtime pool. The existing two-hand gate/active shield filter and native layer transaction need coordinated changes;an isolated CanEquip exemption would be incomplete.',
                               'No production C#,runtime data or accepted DLL changes. No new gameplay/native test result is claimed. Effects/audio and talent/hotkey icons remain separately tracked.'])
    target = ROOT / 'tools/reference/warlock-hands-reference-evidence.json'
    encoded = json.dumps(result, indent=2) + '\n'
    if check:
        assert target.read_text() == encoded
    else:
        target.write_text(encoded)
    print('Warlock hand metadata:8classes/128stats/16rules;109type codes/218parents;Grimoire index106;production and protected saves unchanged;runtime hand rule remains to implement.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--check', action='store_true')
    probe(parser.parse_args().check)
