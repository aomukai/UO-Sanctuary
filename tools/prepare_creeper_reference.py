#!/usr/bin/env python3
"""Pin current Poison Creeper data and bounded older source before implementation."""
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
    rows, pins, corroborated, quoting = {}, {}, 0, []
    requests = [
        ('skills', 'skills.txt', 'skill', [('222', 'Plague Poppy'), ('294', 'Vine Attack')]),
        ('monstats', 'monstats-sentries-verified.txt', 'Id', [('plaguepoppy', 'plaguepoppy')]),
        ('monstats2', 'monstats2-sentries-verified.txt', 'Id', [('plaguepoppy', 'plaguepoppy')]),
        ('missiles', 'missiles.txt', 'Missile', [('plague vines', 'plague vines'), ('plague vines trail', 'plague vines trail')]),
        ('states', 'states-creeper-verified.txt', 'state', [('vine_beast', 'vine_beast')])]
    for table, filename, key, identities in requests:
        raw = local_root / filename
        local = list(csv.DictReader(raw.read_text(encoding='utf-8-sig').splitlines(), delimiter='\t'))
        cached_path = source / 'json' / (table + '.json')
        assert sha(cached_path) == manifest['files']['json/' + table + '.json']['sha256']
        cached = json.loads(cached_path.read_text())
        pins[table] = dict(archivePath='data:data/global/excel/' + table + '.txt', localFilename=filename, sha256=sha(raw))
        rows[table] = {}
        for cached_key, identity in identities:
            row = next(x for x in local if x[key] == identity)
            rows[table][identity] = {k: v for k, v in row.items() if k and v != ''}
            for field, value in cached[cached_key].items():
                if field in row and field != 'lineNumber':
                    compared = str(value)
                    if table == 'skills' and field in ['calc1', 'cltcalc1'] and compared.startswith('"') and compared.endswith('"'):
                        quoting.append(dict(table=table, identity=identity, field=field, cached=compared, local=row[field], rule='Strip one cached outer TSV-quoting pair for formula comparison only.'))
                        compared = compared[1:-1]
                    assert compared == row[field], (table, identity, field, value, row[field])
                    corroborated += 1
    skill = rows['skills']['Plague Poppy']
    attack = rows['skills']['Vine Attack']
    monster = rows['monstats']['plaguepoppy']
    body = rows['monstats2']['plaguepoppy']
    head, trail = (rows['missiles'][name] for name in ['plague vines', 'plague vines trail'])
    assert [skill[k] for k in ['srvdofunc', 'summon', 'pettype', 'petmax', 'mana', 'InTown', 'sumskill1', 'sumsk1calc']] == ['115', 'plaguepoppy', 'vine', '1', '8', '1', 'Vine Attack', 'lvl']
    assert [monster[k] for k in ['AI', 'Skill1', 'Sk1mode', 'noRatio', 'minHP', 'maxHP']] == ['Vines', 'Vine Attack', 'S1', '1', '50', '50']
    assert body['SizeX'] == body['SizeY'] == '0'
    assert attack['srvdofunc'] == '130' and attack['srvmissilea'] == 'plague vines'
    assert head['pSrvDoFunc'] == '26' and head['SubMissile1'] == 'plague vines trail'
    assert trail['pSrvHitFunc'] == '50' and trail['Skill'] == 'Plague Poppy'
    # This is a tooltip check, not a proof of NPC damage/stat ownership.
    minimum = (int(skill['EMin']) << int(skill['HitShift'])) * int(skill['ELen']) // 256
    maximum = (int(skill['EMax']) << int(skill['HitShift'])) * int(skill['ELen']) // 256
    assert (minimum, maximum, int(skill['ELen']) / 25) == (12, 18, 4)
    animation_path = local_root / 'AnimData.d2'
    animation_pin = json.loads((ROOT / 'tools/reference/d2r-local-animation-evidence.json').read_text())
    assert sha(animation_path) == animation_pin['sha256']
    raw, offset, animations = animation_path.read_bytes(), 0, []
    for _ in range(256):
        count = struct.unpack_from('<I', raw, offset)[0]; offset += 4
        for _ in range(count):
            code = raw[offset:offset + 8].split(b'\0')[0].decode('ascii')
            frames, speed = struct.unpack_from('<II', raw, offset + 8)
            if code.startswith('K9'):
                animations.append(dict(code=code, frames=frames, speed=speed,
                                       events=[[i, v] for i, v in enumerate(raw[offset + 16:offset + 160]) if v]))
            offset += 160
    assert offset == len(raw) and len(animations) == 14
    stage = ROOT / '.local/class-skills'
    tree = json.loads((stage / 'd2moo-source-tree.json').read_text())
    assert tree['sha'] == '0c0bbd1e6bef9181a3da1c046523af622a925416'
    hashes, routines = {}, {}
    for relative, names in [
        ('source/D2Game/src/SKILLS/SkillDruid.cpp', ['SKILLS_SrvDo115_Vines', 'SKILLS_SrvDo130_VineAttack']),
        ('source/D2Game/src/AI/AiThink.cpp', ['D2GAME_AI_Unk_110_111_6FCECC40', 'AITHINK_Fn110_Vines']),
        ('source/D2Game/src/MISSILES/MissMode.cpp', ['MISSMODE_SrvDo26_Vines_PlagueVines', 'MISSMODE_SrvHit50_PlagueVinesTrail']),
        ('source/D2Game/src/SKILLS/SkillNec.cpp', ['D2GAME_SetSummonPassiveStats_6FD0C530']),
        ('source/D2Game/src/SKILLS/Skills.cpp', ['D2GAME_SummonPet_6FD14430'])]:
        data = (stage / 'd2moo-pinned-source' / relative).read_bytes()
        assert hashlib.sha1(b'blob ' + str(len(data)).encode() + b'\0' + data).hexdigest() == next(x['sha'] for x in tree['tree'] if x['path'] == relative)
        hashes[relative] = hashlib.sha256(data).hexdigest()
        text = data.decode()
        for name in names:
            match = re.search(r'^.*\b' + re.escape(name) + r'\([^;\n]*\)\s*\n\{', text, re.M)
            assert match, name
            end, depth = text.index('{', match.start()) + 1, 1
            while depth:
                depth += (text[end] == '{') - (text[end] == '}'); end += 1
            routines[name] = hashlib.sha256(text[match.start():end].encode()).hexdigest()
    evidence = dict(
        referenceVersion='3.3.93847', handlerEnabled=False, installed=False,
        scope='Current local table/animation and supplementary legacy source preflight. No Creeper actor or behavioral/native acceptance is claimed.',
        executableSha256=prior['executable']['sha256'], buildInfoSha256=prior['buildInfo']['sha256'],
        primaryDataCommit=manifest['commit'], legacyCommit=tree['sha'], localTables=pins,
        currentRows=rows, corroboratedFields=corroborated, cachedQuotingNormalizations=quoting, sourceHashes=hashes, reviewedRoutineHashes=routines,
        animation=dict(sha256=sha(animation_path), records=animations, scope='Raw K9 metadata only; actor/skill scheduling and modern rate remain to verify.'),
        userObservation=dict(date='2026-10-27', referenceVersion='3.3.93847', effectiveRank=1, mana=8,
                             life=50, minimum=12, maximum=18, poisonSeconds=4,
                             scope='User has no allocated point and reports the rank-one tooltip preview. This corroborates display values, not a spawned pet or combat behavior.'),
        established=[
            'Current player222/do115 is right-only SC,8mana,InTown1,one shared vine pet and inherited Vine Attack rank equal to effective summon rank. Life formula is50percent per rank above1; explicit level formula is ownerLevel*3/4+rank.',
            'Legacy do115 uses the shared summon factory with mode8, updates summon AI, toggles VINE_BEAST, sets explicit level and invokes shared passives. It does not call the explicit-level base-stat helper used by Raven; do not silently copy that birth-stat path.',
            'Current rank-one poison endpoints32/48FP256 per frame times100frames display12/18whole damage over4seconds, matching the user preview. Hard Rabies adds10percent per point in the row; pet/owner damage and synergy transfer still need composition.',
            'Legacy Vines AI has distinct owner-distance35warp, town handling, sight cutoff20,100frame strict skill delay and25frame stall. Already-poisoned targets or exactly100poison resistance trigger escape; movement/follow services and modern equivalence remain unverified.',
            'Legacy Vine Attack emits min(3+rank-1,24)heads with four cardinal offsets and the existing Charged Bolt initializer. Head do26 requests a fresh trail when remainingFrames modulo max(Param1,1) is0; current Param1is9. Trail has its own Plague Poppy skill field, Size3, LastCollide and hit50 early-window return path.',
            'The actual local trail Range is the expression100+(lvl*25), not an integer constant. It must not be silently fed to the old constructor integer wRange/LevRange route. Current parser/rank/lifetime and factory ownership/damage composition require dedicated proof.',
            'Current K9S1HTH is2raw frames at256speed, with kind2at index1. This differs from Raven A1kind1; raw metadata does not establish skill dispatch order or current animation scheduling.',
            'Current vine_beast state150 has active1,setfunc11 and client activefunc89. It has no directly declared ordinary invulnerability flag. Do not infer permanent Raven-like invulnerability from zero source footprint or underground appearance.'
        ], reuse=[
            'Saved minion ownership/history permission, facet rescale with preserved life percentage, FIFO/category caps, shared town-capable SC casting/payment/cancellation and right/quick input.',
            'Source arithmetic, shared summon passives, poison state/mitigation/kill-credit, source movement/Charged Bolt initialization and missile lifecycle components only where the complete route matches.'
        ], remaining=[
            'Resolve actual spawn/base defense/AR/life regeneration before and after explicit level/passive HP changes; do not use Raven birth stats by analogy.',
            'Compose current VINE_BEAST state visibility/attackability/skill-mode/target slow, owner follow/escape/warp/AI ordering and observed modern timing.',
            'Trace actual head/trail creation, row skill override versus supplied Vine Attack294, inherited Plague Poppy/Rabies/pierce stats, Range expression evaluation and collision/hit50 meaning before implementing damage.',
            'Implement and verify the actual saved pet/mats, shared vine replacement, resource/cancellation/input, death/offline/facet/class transitions and two actual reloads. Individual uncertain branches remain nonblocking for unaffected work.',
            'Final Creeper body/underground/surface/attack/plague-vines/mat/wither/audio effects and independent talent/right/quick icons remain in the existing inventories.'
        ])
    output = ROOT / 'tools/reference/creeper-preflight.json'
    text = json.dumps(evidence, indent=2) + '\n'
    if check:
        assert output.read_text() == text
    else:
        output.write_text(text)
    print(f'Creeper preflight: {corroborated}current fields,14K9animation records,8legacy routines and rank-one user tooltip; no handler or actor claimed.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--local-root', type=Path, required=True)
    parser.add_argument('--installation', type=Path, required=True)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    build(args.local_root, args.installation, args.check)
