#!/usr/bin/env python3
"""Pin Heart of Wolverine's current local rows and bounded older source before rollout."""
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
        ('skills', 'skills.txt', 'skill', [('236', 'Heart of Wolverine'), ('297', 'Wolverine Aura')]),
        ('monstats', 'monstats-sentries-verified.txt', 'Id', [('heartofwolverine', 'heartofwolverine')]),
        ('monstats2', 'monstats2-sentries-verified.txt', 'Id', [('heartofwolverine', 'heartofwolverine')]),
        ('states', 'states-creeper-verified.txt', 'state', [('wolverine', 'wolverine'), ('wolverinecontrol', 'wolverinecontrol')])]:
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
    summon, aura = rows['skills']['Heart of Wolverine'], rows['skills']['Wolverine Aura']
    assert [summon[k] for k in ['srvdofunc', 'summon', 'pettype', 'petmax', 'summode', 'sumskill1', 'sumsk1calc', 'calc1', 'calc2']] == ['119', 'heartofwolverine', 'totem', '1', 'NU', 'Wolverine Aura', 'lvl', '(lvl-1)*par1', 'ulvl']
    assert [aura[k] for k in ['srvdofunc', 'aurafilter', 'aurastate', 'auratargetstate', 'aurarangecalc', 'aurastat1', 'aurastatcalc1', 'aurastat2', 'aurastatcalc2']] == ['65', '65795', 'wolverinecontrol', 'wolverine', 'ln12', 'item_tohit_percent', 'ln34', 'damagepercent', 'ln56']
    assert [aura['Param' + str(i)] for i in range(1, 7)] == ['30', '2', '25', '7', '20', '7']
    body = rows['monstats']['heartofwolverine']
    assert [body[k] for k in ['noRatio', 'minHP', 'maxHP', 'AC', 'DamageRegen', 'Code']] == ['1', '136', '136', '123', '3', 'x3']
    assert summon['Param1'] == '25' and summon['mana'] == '20' and summon['lvlmana'] == '1'
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
            if code.startswith('X3'):
                records.append(dict(code=code, frames=frames, speed=speed, events=[[i, v] for i, v in enumerate(raw[offset + 16:offset + 160]) if v]))
            offset += 160
    assert offset == len(raw) and records
    assert next(r for r in records if r['code'] == 'X3WLHTH')['speed'] == 232
    stage = ROOT / '.local/class-skills'
    tree = json.loads((stage / 'd2moo-source-tree.json').read_text())
    assert tree['sha'] == '0c0bbd1e6bef9181a3da1c046523af622a925416'
    hashes, routines = {}, {}
    for relative, names in [
        ('source/D2Game/src/SKILLS/SkillDruid.cpp', ['SKILLS_SrvDo119_DruidSummon']),
        ('source/D2Game/src/SKILLS/SkillNec.cpp', ['D2GAME_SKILLS_SetSummonBaseStats_6FD0CB10', 'D2GAME_SetSummonPassiveStats_6FD0C530']),
        ('source/D2Game/src/SKILLS/SkillPal.cpp', ['SKILLS_SrvDo065_BasicAura', 'SKILLS_AuraCallback_BasicAura']),
        ('source/D2Game/src/AI/AiThink.cpp', ['AITHINK_Fn109_Totem'])]:
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
        scope='Heart preparation using current licensed local data and reviewed shared do119/do65/Totem source; no enabled Heart actor or modern behavioral certificate.',
        executableSha256=prior['executable']['sha256'], buildInfoSha256=prior['buildInfo']['sha256'],
        primaryDataCommit=manifest['commit'], legacyCommit=tree['sha'], localTables=pins,
        currentRows=rows, corroboratedFields=compared, sourceHashes=hashes, reviewedRoutineHashes=routines,
        animation=dict(sha256=sha(animation), records=records, scope='Raw X3 metadata only; exact modern controller timing unverified.'),
        established=[
            'Player236/do119,20+rank-1mana,right-only SC,town-capable,shared totem cap1. Explicit owner level,25percent birth life per rank above1 and two100poison passives share the reviewed Oak creation routines.',
            'Current noRatio body has136life/123defense/zero AR and DamageRegen3. Regeneration seeds25FP per frame before the birth-life multiplier. Difficulty/game-context monlvl adds defense/AR; inherited NPC aura rank remains distinct from immutable birth stats.',
            'NPC297/do65 uses filter0x10103,radius30+2*(rank-1),control wolverinecontrol and recipient wolverine. The two aura stats are additive item_tohit_percent25+7*(rank-1) and damagepercent20+7*(rank-1),not maximum life.',
            'X3WLHTH walking metadata is7frames/speed232,where Oak XWWLHTH uses7frames/speed128. Parameterize the shared movement rate; do not copy Oak animation constants. Current Totem AI fields match Oak across three difficulties; source outer policy and shared pet motion may be reused only after actual Heart actor and state/stat consumers are connected and verified.',
            'Source/current-table pins establish reference preparation only. No Heart handler,actor,aura recipient or cast is enabled by this file.'
        ], remaining=[
            'Reuse shared creation/Totem/save/cast and spirit replacement services while preserving Heart birth bases,25percent scaling and its own control/recipient states. Avoid cloning Oak life-specific resource callbacks.',
            'Connect AR and physical damage percentages to existing player and summon consumers with correct additive pools. Verify self/recipient rank rules,periodic expiry,Command,injury/facet/offline/death and saved states before learned rollout.',
            'Oak current-aidel first-think,target/geometry/path/global-event limitations remain explicit; source AStar is unsupported. Item-only permission,exceptional NPC bonuses,PvP/unmapped recipients and actual graphical-client acceptance remain separate.',
            'Final X3body/emergence/movement/hurt/death,Wolverine aura/control overlays and spirit/summon audio remain pending effects. Talent/right/quick icons are tracked independently.'
        ])
    path = ROOT / 'tools/reference/heart-of-wolverine-preflight.json'
    rendered = json.dumps(evidence, indent=2) + '\n'
    if check:
        assert path.read_text() == rendered
    else:
        path.write_text(rendered)
    print(f'Heart of Wolverine: {compared}current fields,{len(records)}X3animations,6shared legacy routines pinned; Heart handler remains disabled.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--local-root', type=Path, required=True)
    parser.add_argument('--installation', type=Path, required=True)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    build(args.local_root, args.installation, args.check)
