#!/usr/bin/env python3
"""Pin current player-form tables and complete legacy routines; no runtime changes."""
import argparse
import csv
import hashlib
import json
from pathlib import Path
import re
import struct

from review_weapon_event_order import function

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / '.local/class-skills'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def build(local_root, installation, check=False):
    beasts = json.loads((ROOT / 'tools/reference/druid-beasts-preflight.json').read_text())
    assert sha(installation / 'D2R.exe') == beasts['executableSha256']
    assert sha(installation / '.build.info') == beasts['buildInfoSha256']
    public = ROOT / 'tools/reference/d2r-named-equipment'
    manifest = json.loads((public / 'sources.json').read_text())
    rows, pins, compared = {}, {}, 0
    for table, filename, key, names in [
        ('skills', 'skills.txt', 'skill', ['Wearwolf', 'Shape Shifting', 'Wearbear']),
        ('states', 'states-creeper-verified.txt', 'state', ['wolf', 'bear']),
        ('monstats', 'monstats-sentries-verified.txt', 'Id', ['wolf', 'bear']),
        ('monstats2', 'monstats2-sentries-verified.txt', 'Id', ['wolf', 'bear']),
    ]:
        primary = local_root / filename
        local = list(csv.DictReader(primary.read_text(encoding='utf-8-sig').splitlines(), delimiter='\t'))
        path = public / 'json' / (table + '.json')
        assert sha(path) == manifest['files']['json/' + table + '.json']['sha256']
        cached = json.loads(path.read_text())
        rows[table] = {}
        for name in names:
            actual = next(r for r in local if r.get(key) == name)
            prior = next(r for r in cached.values() if r.get(key) == name)
            rows[table][name] = {k: v for k, v in actual.items() if k and v != ''}
            for field, value in prior.items():
                if field not in actual or field == 'lineNumber':
                    continue
                value = str(value)
                if table == 'skills' and value.startswith('"') and value.endswith('"'):
                    value = value[1:-1]
                assert value == actual[field], (table, name, field, value, actual[field])
                compared += 1
        pins[table] = dict(filename=filename, sha256=sha(primary), publicSha256=sha(path))
    for skill, state, index, code in [('Wearwolf', 'wolf', '430', '40'), ('Wearbear', 'bear', '431', 'TG')]:
        row = rows['skills'][skill]
        assert [row[k] for k in ['srvdofunc', 'aurastate', 'mana', 'manashift', 'globaldelay', 'anim', 'seqnum', 'InTown', 'leftskill', 'rightskill']] == ['116', state, '15', '8', '25', 'SC', '22', '1', '0', '1']
        assert row['auralencalc'] == "1000+skill('Shape Shifting'.ln12)"
        assert rows['states'][state]['gfxclass'] == rows['monstats'][state]['*hcIdx'] == index
        assert rows['monstats'][state]['Code'] == code
        assert all(rows['states'][state][field] == '1' for field in ['transform', 'restrict', 'disguise', 'meleeonly'])
        assert rows['states'][state]['group'] == '3'
    lycan = rows['skills']['Shape Shifting']
    assert [lycan[k] for k in ['passive', 'Param1', 'Param2', 'Param3', 'Param4']] == ['1', '1000', '500', '20', '5']

    animation_path = local_root / 'AnimData.d2'
    assert sha(animation_path) == beasts['animation']['sha256']
    raw, offset, animations = animation_path.read_bytes(), 0, []
    for _ in range(256):
        count = struct.unpack_from('<I', raw, offset)[0]
        offset += 4
        for _ in range(count):
            code = raw[offset:offset + 8].split(b'\0')[0].decode('ascii')
            frames, speed = struct.unpack_from('<II', raw, offset + 8)
            if code.startswith(('40', 'TG')):
                animations.append(dict(code=code, frames=frames, speed=speed,
                    events=[[i, v] for i, v in enumerate(raw[offset + 16:offset + 160]) if v]))
            offset += 160
    assert offset == len(raw) and len(animations) == 22
    tree = json.loads((BASE / 'd2moo-source-tree.json').read_text())
    assert tree['sha'] == beasts['legacyCommit']
    hashes, routines = {}, {}
    for relative, names in [
        ('source/D2Game/src/SKILLS/SkillDruid.cpp', ['SKILLS_SrvDo116_Wearwolf_Wearbear', 'sub_6FCFE030', 'sub_6FCFE0E0', 'sub_6FD00370']),
        ('source/D2Game/src/SKILLS/Skills.cpp', ['sub_6FD10CE0', 'D2GAME_SKILLMANA_Consume_6FD10A50', 'sub_6FD11BA0', 'D2GAME_SKILLS_SetDelay_6FD11C00', 'sub_6FD11C90', 'D2GAME_SKILLS_Handler_6FD12BA0']),
        ('source/D2Common/src/DataTbls/SequenceTbls.cpp', []),
    ]:
        raw = (BASE / 'd2moo-pinned-source' / relative).read_bytes()
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(r['sha'] for r in tree['tree'] if r['path'] == relative)
        hashes[relative] = hashlib.sha256(raw).hexdigest()
        for name in names:
            routines[name] = hashlib.sha256(function(raw.decode(), name).encode()).hexdigest()
    text = (BASE / 'd2moo-pinned-source/source/D2Common/src/DataTbls/SequenceTbls.cpp').read_text()
    body = re.search(r'D2AnimSeqTxt gPlayerSequenceUnmorph\[16\] =\s*\{(.*?)\n\};', text, re.S).group(1)
    entries = re.findall(r'\{\s*0,\s*(\w+),\s*(\d+),\s*0,\s*(\w+)\s*\}', body)
    assert len(entries) == 16 and [int(r[1]) for r in entries] == list(reversed(range(16)))
    assert all(r[0] == 'PLRMODE_SPECIAL1' for r in entries)
    assert [i for i, r in enumerate(entries) if r[2] != 'ANIMSEQ_EVENT_NONE'] == [13]
    table = re.search(r'gPlayerWeaponsSequenceTable\[24\] =\s*\{(.*?)\n\};', text, re.S).group(1)
    slots = [s.strip() for s in table.split(',') if s.strip()]
    assert slots[22] == '&gPlayerWeaponsSequenceUnmorph'
    observation_path = ROOT / 'tools/reference/druid-forms-user-observations.json'
    observations = json.loads(observation_path.read_text())
    assert observations['observed']['sameFormRecast'] == 'Returns to human without mana cost'
    assert observations['observed']['enteringFormLife'] == 'Preserves current life percentage'
    result = dict(referenceVersion='3.3.93847', scope='Current-data and complete legacy-source preparation; no learned player forms or native equivalence accepted',
        handlerEnabled=False, installed=False, executableSha256=beasts['executableSha256'], buildInfoSha256=beasts['buildInfoSha256'],
        primaryDataCommit=manifest['commit'], legacyCommit=tree['sha'], localTables=pins, currentRows=rows, corroboratedFields=compared,
        animation=dict(sha256=sha(animation_path), records=animations), sourceHashes=hashes, reviewedRoutineHashes=routines,
        reverseSequence=dict(index=22, rows=[dict(mode=r[0], frame=int(r[1]), event=r[2]) for r in entries]),
        userReportSha256=sha(observation_path), established=[
            'Current Werewolf223/Werebear228 use do116, right-only town-enabled SC,15mana and25-frame global delay. Lycanthropy224 has no independent state/passive writer; form formulas read its duration/life terms.',
            'Wolf/bear states139/140 share exclusive group3, transform/disguise/melee-only flags and setfunc14. They map to player-form models430/code40 and431/codeTG, distinct from summoned spiritwolf/fenris/druidbear.',
            'Complete legacy do116 first removes active same-group states and returns0 after applying delay; the ordinary handler returns before mana consumption on that path. Its separate mana guard permits do116while already shifted. The user confirms free same-form unshift in the pinned build.',
            'New legacy form allocates a timed state, posts/toggles it, schedules removal, applies up to six formula stats, records skill/rank and selects skill mode10. Attackrate also writes OTHER_ANIMRATE. Removal restores the player skill to CAST; these full routines are pinned for subsequent executable comparisons.',
            'Legacy sequence22 is a16-entry reversed S1animation15..0with an action at index13. Current SC,UseAttackRate and form metadata must be composed with the live form conversion path before timing acceptance.',
            'The user confirms entering a form preserves relative current life. Existing proportional player-resource helpers are reuse candidates; this observation does not separately establish voluntary/expired/low-life return behavior.',
        ], remaining=[
            'No native saved form/life/stamina/AR/damage/defense/concentration/rate/restriction consumer, casting/input/payment/delay or reload acceptance is claimed. Reuse existing player stat, resource, weapon, animation and persistence services where semantics agree.',
            'Direct cross-form selection, modern rank1payment versus legacy minimum-mana quirk, stat capture/refresh,life adjustment/expiry/low-life unshift,global versus local delay and post-2.4attack-rate composition require further source/current-build corroboration.',
            'Form-specific weapon/skill restrictions and movement/block/hurt/death/cast animation clocks must use their own metadata. Pinning legacy routines is not current executable equivalence.',
            'Final transformation/body/motion/contact/audio assets and separate Werewolf/Werebear talent+right/quick icons remain pending. Lycanthropy needs a talent icon only. No class is complete.',
        ])
    path = ROOT / 'tools/reference/druid-forms-preflight.json'
    rendered = json.dumps(result, indent=2) + '\n'
    if check:
        assert path.read_text() == rendered
    else:
        path.write_text(rendered)
    print(f'Druid forms preparation:{compared}current fields,{len(animations)}animations,{len(routines)}complete routines,16reverse-sequence entries; no handler enabled.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--local-root', type=Path, required=True)
    parser.add_argument('--installation', type=Path, required=True)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    build(args.local_root, args.installation, args.check)
