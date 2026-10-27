#!/usr/bin/env python3
"""Pin Spirit of Barbs's current local rows and bounded older source before rollout."""
import argparse
import csv
import hashlib
import json
from pathlib import Path
import re
import struct

ROOT = Path(__file__).resolve().parents[1]


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def build(local_root, installation, check=False):
    prior = json.loads((ROOT / 'tools/reference/d2r-local-installation-evidence.json').read_text())
    assert sha(installation / 'D2R.exe') == prior['executable']['sha256']
    assert sha(installation / '.build.info') == prior['buildInfo']['sha256']
    source = ROOT / 'tools/reference/d2r-named-equipment'
    manifest = json.loads((source / 'sources.json').read_text())
    assert manifest['commit'] == 'fa1286949266498b294c40c2942521a7c0d7207a'
    rows, pins, compared = {}, {}, 0
    for table, filename, key, names in [
        ('skills', 'skills.txt', 'skill', [('246', 'Spirit of Barbs'), ('296', 'Barbs Aura')]),
        ('monstats', 'monstats-sentries-verified.txt', 'Id', [('spiritofbarbs', 'spiritofbarbs')]),
        ('monstats2', 'monstats2-sentries-verified.txt', 'Id', [('spiritofbarbs', 'spiritofbarbs')]),
        ('states', 'states-creeper-verified.txt', 'state', [('barbs', 'barbs'), ('barbscontrol', 'barbscontrol')]),
        ('itemstatcost', 'itemstatcost-barbs-verified.txt', 'Stat', [('item_attackertakesdamage', 'item_attackertakesdamage')])]:
        path = local_root / filename
        local = list(csv.DictReader(path.read_text(encoding='utf-8-sig').splitlines(), delimiter='\t'))
        cache = source / 'json' / (table + '.json')
        assert sha(cache) == manifest['files']['json/' + table + '.json']['sha256']
        cached = json.loads(cache.read_text())
        rows[table] = {}
        for identity, name in names:
            row = next(x for x in local if x[key] == name)
            rows[table][name] = {k: v for k, v in row.items() if k and v != ''}
            for field, value in cached[identity].items():
                if field in row and field != 'lineNumber':
                    assert str(value) == row[field], (table, name, field, value, row[field])
                    compared += 1
        pins[table] = dict(archivePath='data:data/global/excel/' + table + '.txt', localFilename=filename, sha256=sha(path))
    summon, aura = rows['skills']['Spirit of Barbs'], rows['skills']['Barbs Aura']
    assert [summon[k] for k in ['srvdofunc', 'summon', 'pettype', 'petmax', 'summode', 'sumskill1', 'sumsk1calc', 'calc1', 'calc2']] == ['119', 'spiritofbarbs', 'totem', '1', 'NU', 'Barbs Aura', 'lvl', '(lvl-1)*par1', 'ulvl']
    assert [aura[k] for k in ['srvdofunc', 'aurafilter', 'aurastate', 'auratargetstate', 'aurarangecalc', 'aurastat1', 'aurastatcalc1']] == ['65', '65795', 'barbscontrol', 'barbs', 'ln12', 'item_attackertakesdamage', 'edmn']
    assert [aura['Param' + str(i)] for i in range(1, 3)] == ['30', '2'] and 'aurastat2' not in aura
    assert [aura[k] for k in ['EMin', 'EMinLev1', 'EMinLev2', 'EMinLev3', 'EMinLev4', 'EMinLev5', 'HitShift']] == ['32', '9', '18', '27', '36', '45', '8']
    assert [summon[k] for k in ['MinDam', 'MinLevDam1', 'MinLevDam2', 'MinLevDam3', 'MinLevDam4', 'MinLevDam5', 'HitShift']] == ['32', '9', '18', '27', '36', '45', '8']
    item = rows['itemstatcost']['item_attackertakesdamage']
    assert [item[k] for k in ['*ID', 'fCallback', 'itemevent1', 'itemeventfunc1']] == ['78', '1', 'attackedinmelee', '6']
    body = rows['monstats']['spiritofbarbs']
    assert [body[k] for k in ['noRatio', 'minHP', 'maxHP', 'AC', 'DamageRegen', 'Code']] == ['1', '213', '213', '196', '3', 'x4']
    assert summon['Param1'] == '25' and summon['mana'] == '25' and summon['lvlmana'] == '1'
    oak = json.loads((ROOT / 'tools/reference/oak-sage-preflight.json').read_text())['currentRows']['monstats']['oaksage']
    assert body['AI'] == oak['AI'] == 'Totem'
    for field in ['aidel', 'aip1', 'aip2', 'aip3', 'aip4', 'aip5']:
        for suffix in ['', '(N)', '(H)']:
            assert body[field + suffix] == oak[field + suffix]

    animation = local_root / 'AnimData.d2'
    anim_pin = json.loads((ROOT / 'tools/reference/d2r-local-animation-evidence.json').read_text())
    assert sha(animation) == anim_pin['sha256']
    raw, offset, records = animation.read_bytes(), 0, []
    for _ in range(256):
        count = struct.unpack_from('<I', raw, offset)[0]; offset += 4
        for _ in range(count):
            code = raw[offset:offset + 8].split(b'\0')[0].decode('ascii')
            frames, speed = struct.unpack_from('<II', raw, offset + 8)
            if code.startswith('X4'):
                records.append(dict(code=code, frames=frames, speed=speed, events=[[i, v] for i, v in enumerate(raw[offset + 16:offset + 160]) if v]))
            offset += 160
    assert offset == len(raw) and records
    assert next(r for r in records if r['code'] == 'X4WLHTH')['speed'] == 248
    stage = ROOT / '.local/class-skills'
    tree = json.loads((stage / 'd2moo-source-tree.json').read_text())
    assert tree['sha'] == '0c0bbd1e6bef9181a3da1c046523af622a925416'
    hashes, routines = {}, {}
    for relative, names in [
        ('source/D2Game/src/SKILLS/SkillDruid.cpp', ['SKILLS_SrvDo119_DruidSummon']),
        ('source/D2Game/src/SKILLS/SkillNec.cpp', ['D2GAME_SKILLS_SetSummonBaseStats_6FD0CB10', 'D2GAME_SetSummonPassiveStats_6FD0C530']),
        ('source/D2Game/src/SKILLS/SkillPal.cpp', ['SKILLS_SrvDo065_BasicAura', 'SKILLS_AuraCallback_BasicAura']),
        ('source/D2Game/src/AI/AiThink.cpp', ['AITHINK_Fn109_Totem']),
        ('source/D2Game/src/SKILLS/SkillItem.cpp', ['SKILLITEM_EventFunc06_AttackerTakesDamage'])]:
        raw = (stage / 'd2moo-pinned-source' / relative).read_bytes()
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(x['sha'] for x in tree['tree'] if x['path'] == relative)
        hashes[relative] = hashlib.sha256(raw).hexdigest(); text = raw.decode()
        for name in names:
            match = re.search(r'^.*\b' + re.escape(name) + r'\([^;\n]*\)\s*\n\{', text, re.M)
            assert match, name
            end, depth = text.index('{', match.start()) + 1, 1
            while depth:
                depth += (text[end] == '{') - (text[end] == '}'); end += 1
            routines[name] = hashlib.sha256(text[match.start():end].encode()).hexdigest()
    evidence = dict(referenceVersion='3.3.93847', handlerEnabled=False, installed=False,
        scope='Barbs preparation using current licensed tables and reviewed shared creation/aura/AI plus flat-return callback; no enabled Barbs actor or modern behavioral certificate.',
        executableSha256=prior['executable']['sha256'], buildInfoSha256=prior['buildInfo']['sha256'],
        primaryDataCommit=manifest['commit'], legacyCommit=tree['sha'], localTables=pins,
        currentRows=rows, corroboratedFields=compared, sourceHashes=hashes, reviewedRoutineHashes=routines,
        animation=dict(sha256=sha(animation), records=records, scope='Raw X4 metadata only; exact modern controller timing unverified.'),
        established=[
            'Player246/do119,25+rank-1mana,right-only SC,town-capable,shared totem cap1. Explicit owner level,25percent birth life per rank above1 and two100poison passives share the reviewed spirit creation path.',
            'Current noRatio body has213life/196defense/zero AR and DamageRegen3. Regeneration seeds39FP per frame before birth-life scaling. X4WLHTH has7frames/speed248,distinct from Heart232and Oak128. Totem AI parameters match across the three current difficulty columns.',
            'NPC296/do65 uses filter0x10103,radius30+2perrank,control163and recipient147. Its only aura stat is item_attackertakesdamage via edmn. Current minimum32and five tier increments9/18/27/36/45match the player physical-minimum tooltip fields. No thorns_percent stat is supplied by Barbs.',
            'Current licensed itemstatcost78has fCallback1,attackedinmelee and event function6. The complete reviewed older callback reads the aggregate item/skill stat and emits one physical nDamage<<8packet with result0x4021and hit class0x8D. Full event/mitigation dispatch remains a separate acceptance scope.',
            'This preflight enables no Barbs handler,actor,aura or return consumer. Existing Paladin Thorns flat-return machinery is a reuse candidate,not a substitute for aggregate stat and event-order acceptance.'
        ], remaining=[
            'Compose shared birth/Totem/save/cast services with Barbs own states,flat minimum formula,fractional birth life and248walking rate. Preserve old spirit save compatibility and actual shared-category replacement.',
            'Verify aggregation with Paladin flat Thorns and relevant item ATD before mitigation; do not add percentage return to Barbs. Inspect current Life Tap/leech/return order and lethal rescue rather than trusting a historical gap description.',
            'Modern attacked-in-melee dispatch,multiple suppliers,miss/block/zero/lethal/ranged/spell cases,physical mitigation/credit,owner/facet/offline/save and actual current-client acceptance remain to implement or verify. Existing item-only/PvP/unmapped/geometry/AStar limitations stay explicit.',
            'Final X4body/emergence/walk/hurt/death,spirit_of_barbs recipient/control overlays,retaliation contact and spirit/summon audio remain pending effects. Talent/right/quick icons remain separate.'
        ])
    path = ROOT / 'tools/reference/spirit-of-barbs-preflight.json'
    rendered = json.dumps(evidence, indent=2) + '\n'
    if check:
        assert path.read_text() == rendered
    else:
        path.write_text(rendered)
    print(f'Spirit of Barbs: {compared}current fields,{len(records)}X4animations,7reviewed legacy routines pinned; Barbs handler remains disabled.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--local-root', type=Path, required=True)
    parser.add_argument('--installation', type=Path, required=True)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    build(args.local_root, args.installation, args.check)
