#!/usr/bin/env python3
"""Execute complete source skill/missile getters, damage creator and stat writer."""
import argparse
import hashlib
import json
import re
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills'


def build(check=False):
    subprocess.run([sys.executable, str(ROOT / 'tools/probe_poison_javelin_damage_reference.py'), '--check'], check=True, stdout=subprocess.DEVNULL)
    parent = json.loads((ROOT / 'tools/reference/poison-javelin-damage-engine-evidence.json').read_text())
    tree = json.loads((STAGE / 'd2moo-source-tree.json').read_text())
    blocks, hashes = {}, {}

    def routine(relative, name):
        raw = (STAGE / 'd2moo-pinned-source' / relative).read_bytes()
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(x['sha'] for x in tree['tree'] if x['path'] == relative)
        hashes[relative] = hashlib.sha256(raw).hexdigest()
        text = raw.decode()
        match = re.search(r'^.*\b' + re.escape(name) + r'\([^;\n]*\)\s*\n\{', text, re.M)
        assert match, name
        end = text.index('{', match.start()) + 1
        depth = 1
        while depth:
            depth += (text[end] == '{') - (text[end] == '}')
            end += 1
        body = text[match.start():end]
        blocks[name] = hashlib.sha256(body.encode()).hexdigest()
        return body + '\n'

    base = (STAGE / 'poison-javelin-damage-source-probe.cpp').read_text().split('int main()', 1)[0]
    def replace(before, after):
        nonlocal base
        assert base.count(before) == 1, before
        base = base.replace(before, after, 1)
    replace('int nSrcDam=128,nEType=0,', 'unsigned dwFlags[1]={};int dwMinDam=0,dwMaxDam=0,dwMinLvlDam[5]={},dwMaxLvlDam[5]={},dwDmgSymPerCalc=2;int nSrcDam=128,nEType=0,')
    replace('unsigned dwMissileFlags=0;', 'int dwElemMin=0,dwElemMax=0,dwMinElemLev[5]={},dwMaxElemLev[5]={},dwElemDmgSymPerCalc=-1,nHitShift=0;unsigned dwMissileFlags=0;')
    replace('int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int,int,int){return hard*12;}', 'int volcano=0;int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int formula,int,int){return formula==2?volcano*12:hard*8;}')
    for name in ['SKILLS_GetMinPhysDamage', 'SKILLS_GetMaxPhysDamage']:
        replace('int ' + name + '(D2UnitStrc*,int,int,int){return ' + ('skillMin' if 'Min' in name else 'skillMax') + ';}', 'int ' + name + '(D2UnitStrc*,int,int,int);')
    for name in ['MISSILE_GetMinElemDamage', 'MISSILE_GetMaxElemDamage']:
        replace('int ' + name + '(D2UnitStrc*,D2UnitStrc*,int,int){return 0;}', 'int ' + name + '(D2UnitStrc*,D2UnitStrc*,int,int);')
    # KICK/weapon branches are retained but unselected by source229 SrcDam0.
    base += '\nconstexpr int SKILLSFLAGINDEX_KICK=31;\nint STATLIST_GetMinDamageFromUnit(D2UnitStrc*,int){std::abort();}\nint STATLIST_GetMaxDamageFromUnit(D2UnitStrc*,int){std::abort();}\nint MISSILE_EvaluateMissileFormula(D2UnitStrc*,D2UnitStrc*,int,int,int){return hard*8;}\n'
    base = '#include <cstdlib>\n' + base
    common = 'source/D2Common/src/D2Skills.cpp'
    for name in ['SKILLS_GetMinPhysDamage', 'SKILLS_GetMaxPhysDamage']:
        base += routine(common, name)
    for name in ['MISSILE_GetMinElemDamage', 'MISSILE_GetMaxElemDamage']:
        base += routine('source/D2Common/src/Units/Missile.cpp', name)
    source = ROOT / 'tools/reference/d2r-named-equipment'
    manifest = json.loads((source / 'sources.json').read_text())
    pins, tables = {}, {}
    for name in ['skills', 'missiles']:
        raw = (source / f'json/{name}.json').read_bytes()
        pins[name] = hashlib.sha256(raw).hexdigest()
        assert pins[name] == manifest['files'][f'json/{name}.json']['sha256']
        tables[name] = json.loads(raw)
    skill, field = tables['skills']['229'], tables['missiles']['moltenboulderfirepath']
    assert skill['DmgSymPerCalc'] == "skill('Volcano'.blvl)*par7" and skill['EDmgSymPerCalc'] == "skill('Firestorm'.blvl)*par8"
    assert field['EDmgSymPerCalc'] == "skill('Firestorm'.blvl)*8" and not field.get('ApplyMastery')
    base += 'void setup(int kind){skill={};row={};skill.nSrcDam=0;skill.nEType=ELEMTYPE_FIRE;skill.dwELen=0;for(int& v:skill.dwELevLen)v=0;'
    for dest, key in [('dwMinDam','MinDam'),('dwMaxDam','MaxDam'),('dwEMin','EMin'),('dwEMax','EMax'),('nToHitShift','HitShift')]:
        base += f'skill.{dest}={skill[key]};'
    for i in range(5):
        for dest, key in [('dwMinLvlDam','MinLevDam'),('dwMaxLvlDam','MaxLevDam'),('dwEMinLev','EMinLev'),('dwEMaxLev','EMaxLev')]:
            base += f'skill.{dest}[{i}]={skill.get(key+str(i+1),0)};'
    base += 'row.nSkill=kind==1?229:0;row.nSrcDamage=row.nSrcMissDmg=0;row.nElemType=kind==0?ELEMTYPE_NONE:ELEMTYPE_FIRE;row.dwElemDmgSymPerCalc=kind==2?1:-1;'
    base += f'if(kind==2){{row.dwElemMin={field["EMin"]};row.dwElemMax={field["EMax"]};row.nHitShift={field["HitShift"]};'
    for i in range(5):
        base += f'row.dwMinElemLev[{i}]={field.get("MinELev"+str(i+1),0)};row.dwMaxElemLev[{i}]={field.get("MaxELev"+str(i+1),0)};'
    base += '}}\n'
    base += r'''int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;bool first=true;printf("[");
for(int kind:{0,1,2})for(int rank=1;rank<=255;rank++)for(int points:{0,20})for(int volcanic:{0,20})for(int mastery:{0,50})for(int item:{0,99999}){
 owner={};weapon={};missile={};missileData={};setup(kind);weapon.dwUnitType=UNIT_ITEM;missile.dwUnitType=UNIT_MISSILE;missile.dwClassId=kind==0?453:kind==1?452:455;missileData.nSkill=229;missileData.nLevel=rank;hard=points;volcano=volcanic;critical=true;critCalls=0;
 for(int& stat:owner.stats)stat=item;owner.stats[STAT_PASSIVE_FIRE_MASTERY]=mastery;
 D2UnitStrc origin;for(int& stat:origin.stats)stat=99999;D2MissileDamageDataStrc packet;MISSILE_CalculateDamageData(&packet,&owner,&origin,&missile,rank);MISSILE_SetDamageStats(&owner,&missile,&packet,rank);
 printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",kind,rank,points,volcanic,mastery,item,packet.nMinDamage,packet.nMaxDamage,packet.nFireMinDamage,packet.nFireMaxDamage,packet.nFlags,critCalls,missile.stats[STAT_PASSIVE_FIRE_MASTERY]);first=false;
}printf("]\n");}
'''
    path = STAGE / 'molten-boulder-damage-source-probe.cpp'
    path.write_text(base)
    binary = path.with_suffix('')
    subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined', '-fno-sanitize-recover=all', str(path), '-o', str(binary)], check=True)
    run = subprocess.run([str(binary)], capture_output=True)
    (STAGE / 'molten-boulder-damage-sanitizer.log').write_bytes(run.stderr)
    assert run.returncode == 0 and not run.stderr, run.stderr.decode()[-3000:]
    data = json.loads(run.stdout)
    assert len(data) == 12240
    def tier(row, base, growth, rank):
        value, left = row.get(base, 0), rank - 1
        for i, length in enumerate([7,8,6,6,2**30], 1):
            amount = min(left, length)
            value += amount * row.get(growth + str(i), 0)
            left -= amount
        return value
    for kind, rank, hard, volcano, mastery, item, pmin, pmax, fmin, fmax, flags, crits, stored in data:
        expected = []
        for maximum in [False, True]:
            v = tier(skill, 'MaxDam' if maximum else 'MinDam', 'MaxLevDam' if maximum else 'MinLevDam', rank)
            expected.append((v + v * volcano * 12 // 100) << 8 if kind == 1 else 0)
        for maximum in [False, True]:
            if kind == 1:
                v = tier(skill, 'EMax' if maximum else 'EMin', 'EMaxLev' if maximum else 'EMinLev', rank) << 8
                v += v * hard * 8 // 100
                v += v * mastery // 100
            elif kind == 2:
                v = tier(field, 'EMax' if maximum else 'EMin', 'MaxELev' if maximum else 'MinELev', rank)
                v = (v + v * hard * 8 // 100) << 2
            else:
                v = 0
            expected.append(v)
        assert [pmin,pmax,fmin,fmax] == expected, (kind,rank,hard,volcano,mastery,item,expected,[pmin,pmax,fmin,fmax])
        assert (flags,crits,stored) == (0,0,0)
    fixture = STAGE / 'molten-boulder-damage-source-probe.json'
    fixture.write_text(json.dumps(data,separators=(',',':'))+'\n')
    evidence = dict(referenceVersion='3.3.93847',handlerEnabled=False,installed=False,legacyCommit=tree['sha'],
        sourceHashes=hashes,routineHashes=blocks,reusedCreatorEvidence='tools/reference/poison-javelin-damage-engine-evidence.json',
        creatorBlockHashes=parent['creatorBlockHashes'],pinnedInputHashes=pins,
        fixture=dict(path=str(fixture.relative_to(ROOT)),sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(),cases=len(data)),
        established=['All255ranks execute complete physical/elemental skill getters, missile elemental getters, creator/add/scale and setter across separate hard Firestorm/Volcano, owner mastery and unrelated owner/origin item controls.',
            'Emergence453 has no damage despite retained skill229. Rolling452 uses source0 skill229 physical hard-Volcano12percent before HitShift8, fire hard-Firestorm8percent after HitShift8 and then fire mastery once.',
            'Ground455 uses missile endpoints, hard-Firestorm8percent before HitShift2 and no owner mastery. Source0 prevents item weapon/element damage and critical inheritance; no owner passive mastery stat is copied.'],
        limits=['Hard skill formula lookup, prepared owner/item stats, ratio, weapon metadata and empty monster-type stat copying are declared parent services. KICK/weapon endpoint services abort if selected.',
            'This establishes numeric capture, not the modern executable, damage roll/mitigation/event timing, Slow Missiles producer, native spatial geometry or rendered client.'],
        sanitizers='ASan/UBSan exit0 and empty runtime stderr required')
    out = ROOT / 'tools/reference/molten-boulder-damage-source-evidence.json'
    if check:
        assert json.loads(out.read_text()) == evidence
    else:
        out.write_text(json.dumps(evidence,indent=2)+'\n')
    print('Molten Boulder:12240complete source damage creator/getter/writer contexts pass with clean sanitizers.')


if __name__ == '__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--check',action='store_true');build(parser.parse_args().check)
