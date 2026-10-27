#!/usr/bin/env python3
"""Pin Oak Sage's current local rows and bounded older source before rollout."""
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
        ('skills', 'skills.txt', 'skill', [('226', 'Oak Sage'), ('298', 'Oak Sage Aura')]),
        ('monstats', 'monstats-sentries-verified.txt', 'Id', [('oaksage', 'oaksage')]),
        ('monstats2', 'monstats2-sentries-verified.txt', 'Id', [('oaksage', 'oaksage')]),
        ('states', 'states-creeper-verified.txt', 'state', [('oaksage', 'oaksage'), ('oaksagecontrol', 'oaksagecontrol')])]:
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
    summon, aura = rows['skills']['Oak Sage'], rows['skills']['Oak Sage Aura']
    assert [summon[k] for k in ['srvdofunc', 'summon', 'pettype', 'petmax', 'summode', 'sumskill1', 'sumsk1calc', 'calc1', 'calc2']] == ['119', 'oaksage', 'totem', '1', 'NU', 'Oak Sage Aura', 'lvl', '(lvl-1)*par3', 'ulvl']
    assert [aura[k] for k in ['srvdofunc', 'aurafilter', 'aurastate', 'auratargetstate', 'aurarangecalc', 'aurastat1', 'aurastatcalc1']] == ['65', '65795', 'oaksagecontrol', 'oaksage', 'ln12', 'item_maxhp_percent', 'ln34']
    assert [aura['Param' + str(i)] for i in range(1, 5)] == ['30', '2', '30', '5']
    animation = local_root / 'AnimData.d2'
    anim_pin = json.loads((ROOT / 'tools/reference/d2r-local-animation-evidence.json').read_text())
    assert sha(animation) == anim_pin['sha256']
    raw, offset, records = animation.read_bytes(), 0, []
    for _ in range(256):
        count = struct.unpack_from('<I', raw, offset)[0]; offset += 4
        for _ in range(count):
            code = raw[offset:offset + 8].split(b'\0')[0].decode('ascii')
            frames, speed = struct.unpack_from('<II', raw, offset + 8)
            if code.startswith('XW'):
                records.append(dict(code=code, frames=frames, speed=speed, events=[[i, v] for i, v in enumerate(raw[offset + 16:offset + 160]) if v]))
            offset += 160
    assert offset == len(raw) and records
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
        scope='Current licensed local data plus supplementary legacy source; no actor, aura leases or modern behavioral certificate.',
        executableSha256=prior['executable']['sha256'], buildInfoSha256=prior['buildInfo']['sha256'],
        primaryDataCommit=manifest['commit'], legacyCommit=tree['sha'], localTables=pins,
        currentRows=rows, corroboratedFields=compared, sourceHashes=hashes, reviewedRoutineHashes=routines,
        animation=dict(sha256=sha(animation), records=records, scope='Raw XW metadata only; exact modern controller timing unverified.'),
        established=[
            'Player226/do119,15+rank-1mana,right-only SC,town-capable,shared totem cap1. Pet level is owner level, not the default rank+3/4owner or Creeper explicit-level-only path.',
            'Legacy do119 invokes both the explicit-level base helper and shared passives. Current noRatio spawn has60life/49defense/zero attack rating and regeneration3; monlvl AC/TH are added before30percent life per rank above1. Regeneration is seeded before this life multiplier.',
            'Current two passive slots add100poison resistance and100maximum poison resistance. They are not a blanket invulnerability flag. Full incoming resistance cap/state behavior belongs to the actor consumer.',
            'Granted NPC298 aura has radius30+2*(rank-1)source units and30+5*(rank-1)life percent, no mana cost, self control161and recipient149states. Source radius is not a native tile or displayed-yard count.',
            'The same item_maxhp_percent stat used by Battle Orders joins its additive skill-percent pool. Life/stamina/mana resource distinctions must remain intact; Oak only changes maximum life.',
            'Totem AI checks threat/escape before ignore-target roll, then strict owner-distance>30teleport and>20walk/run follow, shared pet move and25frame idle. AI/source RNG/service scheduling remain to implement and verify.'
        ], remaining=[
            'Implement saved actor/owner/facet rescale, shared spirit replacement, learned casting/payment/cancellation and actual right/quick input.',
            'Compose do65recipient filter,self/target leases,periodic refresh/expiry,overlap rank rules,current life/regen callbacks,death/offline/class/facet teardown and two reloads. Do not multiply an existing Battle Orders total.',
            'Complete spirit movement/escape/teleport behavior and source-clock scheduling; keep room/path/native geometry and exact modern timing caveats explicit.',
            'Final XWbody/movement/emergence/death,aura overlays and summon/spirit audio are pending effects; talent/right/quick icons are tracked independently.'
        ])
    path = ROOT / 'tools/reference/oak-sage-preflight.json'
    rendered = json.dumps(evidence, indent=2) + '\n'
    if check:
        assert path.read_text() == rendered
    else:
        path.write_text(rendered)
    print(f'Oak Sage: {compared}current fields,{len(records)}XWanimations,6legacy routines pinned; learned handler remains disabled.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--local-root', type=Path, required=True)
    parser.add_argument('--installation', type=Path, required=True)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    build(args.local_root, args.installation, args.check)
