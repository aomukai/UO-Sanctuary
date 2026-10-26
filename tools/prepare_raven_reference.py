#!/usr/bin/env python3
"""Pin Raven's local current-build tables and supplementary legacy routines."""
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
    manifest = json.loads((ROOT / 'tools/reference/d2r-named-equipment/sources.json').read_text())
    assert manifest['commit'] == 'fa1286949266498b294c40c2942521a7c0d7207a'
    tables, pins, local = {}, {}, {}
    for name, filename, key, identity in [
        ('skills', 'skills.txt', 'skill', 'Raven'),
        ('monstats', 'monstats-sentries-verified.txt', 'Id', 'druidhawk'),
        ('monstats2', 'monstats2-sentries-verified.txt', 'Id', 'druidhawk'),
        ('monprop', 'monprop-raven-verified.txt', 'Id', 'druidhawk')]:
        path = local_root / filename
        rows = list(csv.DictReader(path.read_text(encoding='utf-8-sig').splitlines(), delimiter='\t'))
        row = next(x for x in rows if x[key] == identity)
        local[name] = {k: v for k, v in row.items() if k and v != ''}
        pins[name] = dict(archivePath='data:data/global/excel/' + name + '.txt', localFilename=filename, sha256=sha(path))
        if name != 'monprop':
            cached = ROOT / 'tools/reference/d2r-named-equipment/json' / (name + '.json')
            assert sha(cached) == manifest['files']['json/' + name + '.json']['sha256']
            data = json.loads(cached.read_text())
            tables[name] = data['221' if name == 'skills' else identity]
    skill, monster, body, props = (local[k] for k in ['skills', 'monstats', 'monstats2', 'monprop'])
    assert [skill[k] for k in ['*Id', 'srvdofunc', 'mana', 'summon', 'pettype', 'summode']] == ['221', '114', '6', 'druidhawk', 'raven', 'S1']
    assert [skill[k] for k in ['petmax', 'calc2', 'calc3', 'Param1', 'Param2', 'Param5', 'Param6']] == ['min(lvl,par2)', 'ulvl + par1 + lvl', 'ln56', '-2', '5', '12', '1']
    assert [monster[k] for k in ['AI', 'Skill1', 'SkillDamage', 'aip1', 'aip2', 'aip3', 'aip4', 'aip5']] == ['Raven', 'Raven', 'Raven', '10', '6', '2', '95', '35']
    assert body['SizeX'] == body['SizeY'] == '0'
    assert [props[k] for k in ['prop1', 'min1', 'max1', 'prop1 (N)', 'min1 (N)', 'max1 (N)', 'prop1 (H)', 'min1 (H)', 'max1 (H)']] == ['stupidity', '-1', '-1'] * 3
    for name, fields in [
        ('skills', ['srvdofunc', 'summon', 'pettype', 'summode', 'calc2', 'calc3', 'mana', 'Param1', 'Param2', 'Param5', 'Param6', 'Param8', 'ToHit', 'LevToHit', 'MinDam', 'MaxDam', 'DmgSymPerCalc', 'passivestat1', 'passivecalc1']),
        ('monstats', ['AI', 'Skill1', 'SkillDamage', 'aip1', 'aip2', 'aip3', 'aip4', 'aip5', 'Velocity', 'Run', 'Crit', 'MonProp']),
        ('monstats2', ['SizeX', 'SizeY', 'Height', 'MeleeRng', 'BaseW'])]:
        for field in fields:
            assert str(tables[name].get(field, 0)) == str(local[name].get(field, 0)), (name, field)
    animation_path=local_root/'AnimData.d2'
    animation_provenance=json.loads((ROOT/'tools/reference/d2r-local-animation-evidence.json').read_text())
    assert sha(animation_path)==animation_provenance['sha256']
    raw_animation=animation_path.read_bytes();at=0;animations=[]
    for _ in range(256):
        count=struct.unpack_from('<I',raw_animation,at)[0];at+=4
        for _ in range(count):
            code=raw_animation[at:at+8].split(b'\0')[0].decode('ascii')
            frames,speed=struct.unpack_from('<II',raw_animation,at+8)
            if code.startswith('HK'):
                animations.append(dict(code=code,frames=frames,speed=speed,events=[[i,v] for i,v in enumerate(raw_animation[at+16:at+160]) if v]))
            at+=160
    assert at==len(raw_animation) and len(animations)==6
    attack=next(x for x in animations if x['code']=='HKA1HTH')
    assert [attack[k] for k in ['frames','speed','events']]==[23,240,[[10,1]]]
    stage = ROOT / '.local/class-skills'
    tree = json.loads((stage / 'd2moo-source-tree.json').read_text())
    assert tree['sha'] == '0c0bbd1e6bef9181a3da1c046523af622a925416'
    hashes, routines = {}, {}
    for relative, names in [
        ('source/D2Game/src/SKILLS/SkillDruid.cpp', ['SKILLS_SrvDo114_Raven']),
        ('source/D2Game/src/AI/AiThink.cpp', ['D2GAME_AI_Unk107_6FCED140', 'AITHINK_Fn107_Raven']),
        ('source/D2Game/src/SKILLS/SkillNec.cpp', ['D2GAME_SKILLS_SetSummonBaseStats_6FD0CB10', 'D2GAME_SetSummonPassiveStats_6FD0C530', 'sub_6FD0B9F0']),
        ('source/D2Game/src/SKILLS/Skills.cpp', ['sub_6FD14D20']),
        ('source/D2Game/src/MONSTER/MonsterMode.cpp', ['sub_6FC627B0']),
        ('source/D2Game/src/MONSTER/Monster.cpp', ['MONSTER_InitializeStatsAndSkills']),
        ('source/D2Common/src/Items/ItemMods.cpp', ['ITEMMODS_PropertyFunc01', 'ITEMMODS_AddPropertyToItemStatList', 'D2COMMON_11292_ItemAssignProperty']),
        ('source/D2Game/src/SKILLS/SkillItem.cpp', ['SKILLITEM_EventFunc09_Stupidity'])]:
        raw = (stage / 'd2moo-pinned-source' / relative).read_bytes()
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(x['sha'] for x in tree['tree'] if x['path'] == relative)
        hashes[relative] = hashlib.sha256(raw).hexdigest()
        source = raw.decode()
        for name in names:
            match = re.search(r'^.*\b' + re.escape(name) + r'\([^;\n]*\)\s*\n\{', source, re.M)
            assert match, name
            end, depth = source.index('{', match.start()) + 1, 1
            while depth:
                depth += (source[end] == '{') - (source[end] == '}')
                end += 1
            routines[name] = hashlib.sha256(source[match.start():end].encode()).hexdigest()
    evidence = dict(referenceVersion='3.3.93847', handlerEnabled=False, installed=False,
        status='Current-table/source preflight only; no Raven behavioral or native acceptance is claimed.',
        installationEvidence='tools/reference/d2r-local-installation-evidence.json',
        readerEvidence='tools/reference/d2r-local-animation-evidence.json',
        executableSha256=prior['executable']['sha256'], buildInfoSha256=prior['buildInfo']['sha256'],
        primaryDataCommit=manifest['commit'], legacyCommit=tree['sha'], localTables=pins,
        currentRows=local, sourceHashes=hashes, reviewedRoutineHashes=routines,
        animation=dict(archivePath='data:data/global/animdata.d2',localFilename='AnimData.d2',sha256=sha(animation_path),records=animations,
            scope='Current raw metadata only: action scheduling/rate conversion/native pose and modern AI event ordering remain to verify.'),
        established=[
            'Strict read-only local CASC monprop extraction supplies the previously absent druidhawk property row. All three difficulties supply stupidity=-1. Current local skills/monstats/monstats2 corroborate the selected cached Raven fields; licensed raw files remain local and ignored.',
            'Current rows request6mana, right-only SC, town casting, Raven/druidhawk/S1, maximum min(effective rank,5), level max(1,owner level-2+effective rank),12+(rank-1) hit formula and12percent physical synergy per hard Spirit Wolf/Dire Wolf/Grizzly point. These table formulas do not prove how the modern AI consumes them.',
            'Legacy do114 calls the shared summon factory, clears CANBEATTACKED0x4, zeros HP regeneration, evaluates/clamps pet level, then calls shared base/passive stat suppliers. The generic BaseCreature attackability and regeneration defaults must not silently override those flags.',
            'Legacy AI initializes hit budget on first think from the owner highest Raven skill, not from an explicit cast-rank snapshot. It decrements the budget when requesting an attack animation rather than after confirmed health loss. Owner follow/warp, attack delay/chance/distance and orbit have separate branches; modern equivalence remains unverified.',
            'Legacy monster mode stats call sub14D20 with the live owner highest Raven rank: physical min/max are shifted back to whole life and the toht result is added as flat AR. Do not freeze every attack endpoint at birth or treat the wiki AR percentage label as authoritative for this route. The source base helper retains explicit pet level above99 and clamps only its111-row monlvl lookup.',
            'The signed -1 blind property cannot pass unchanged through NamedControl.ApplyBlind, which currently rejects bonus<=0 and requires a PlayerMobile source. The legacy item event instead accepts any nonzero signed value, uses attacker level/RNG and calls the existing blind curse route. Trace the property/stat/event and pet delivery before reusing the player helper.',
            'Reviewed monster initialization dispatches its monprop through ItemAssignProperty in mode0/list0x40. The property func1 row chooses the fixed equal min/max=-1 without a random range, then AddProperty rejects zero rather than negatives and passes the signed value to additive stat storage. Statlist allocation/aggregation/event subscription and the actual Raven handoff still need integrated acceptance.'
        ], reuse=[
            'Shared learned casting, payment/cancellation/right/quick bindings, source formulas and saved summon FIFO/cap infrastructure where routes agree.',
            'Existing native summon ownership, facet difficulty rescaling with preserved life percentage, hostile targeting, damage/mitigation/kill credit and independent blind state consumers; Raven needs its explicit cannot-be-attacked flag, zero-size flight and hit-budget behavior.'
        ], remaining=[
            'Compose current-row summon factory/base/passive stats, generated properties and blind delivery, hit/defense/physical synergy and immunity-pierce consumers. Resolve modern effective-rank capture/hit-budget/timing/animation/target-command behavior without assuming legacy executable equivalence.',
            'Implement and verify the actual saved Raven actor, capped replacement, casting/resource/input, owner/facet/offline/death/class transitions, natural attacks/exhaustion and two real world reloads. Native movement/flight/geometry/timer/client projections must be identified.',
            'Exact generated monster equipment/property consumers remain a separate earlier gap; Raven only needs its identified property route, not arbitrary item support.',
            'Final summon/bird/flight/attack/blind/retire/audio effects and separate talent/right/quick icons remain in the existing placeholder inventories.'
        ], userObservation=dict(
            date='2026-10-26', referenceVersion='3.3.93847', hardRank=20, approximateItemBonus=9,
            report='User has not counted attack dives. At20hard points and approximately9from items, Ravens last noticeably longer than at a lower skill level.',
            scope='Qualitative rank-dependent persistence observation only; exact effective rank, dive count, attempt-versus-hit consumption and modern AI timing are not measured.'),
        secondaryReference=dict(url='https://diablo.fandom.com/wiki/Raven', suppliedByUser=True,
            accessedDate='2026-10-26', access='Direct fetch restricted; search-indexed page text was available.',
            corroboration='Lists12attacks per Raven at rank1 and31at rank20, increasing by1per rank, and a maximum5Ravens. This agrees with the pinned table formulas.',
            limits='Community page is not pinned to build3.3.93847. Its other claims, including guaranteed blind chance, do not override local data or establish modern event behavior; the inspected legacy blind event clamps chance to99.'),
        questions=[
            'Exact current-build Raven attack-dive budget remains unmeasured and nonblocking. The user confirms noticeably longer persistence at high rank; current table formulas and legacy AI expectations remain separate evidence.'
        ], next='Finish Armageddon full regression and exact-package installation first, then Raven source/native implementation; keep each uncertain branch explicit.')
    out = ROOT / 'tools/reference/raven-preflight.json'
    text = json.dumps(evidence, indent=2) + '\n'
    if check:
        assert out.read_text() == text
    else:
        out.write_text(text)
    print('Raven preflight: current pinned local tables and13legacy routines; signed blind/pet delivery and modern AI remain explicit gates.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--local-root', required=True, type=Path)
    parser.add_argument('--installation', required=True, type=Path)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    build(args.local_root, args.installation, args.check)
