#!/usr/bin/env python3
"""Capture complete damage creator/getters/writer for Armageddon control and ground fire."""
import argparse
import hashlib
import json
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills'


def build(check=False):
    subprocess.run([sys.executable, str(ROOT / 'tools/probe_molten_boulder_damage_reference.py'), '--check'], check=True, stdout=subprocess.DEVNULL)
    parent = json.loads((ROOT / 'tools/reference/molten-boulder-damage-source-evidence.json').read_text())
    base = (STAGE / 'molten-boulder-damage-source-probe.cpp').read_text().split('void setup(', 1)[0]
    before = 'int volcano=0;int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int formula,int,int){return formula==2?volcano*12:hard*8;}'
    assert base.count(before) == 1
    base = base.replace(before, 'int boulder=0,volcano=0;int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int formula,int,int){return formula==2?volcano*18:(hard+boulder)*14;}', 1)
    assert base.count('int MISSILE_EvaluateMissileFormula(D2UnitStrc*,D2UnitStrc*,int,int,int){return hard*8;}') == 1
    base = base.replace('int MISSILE_EvaluateMissileFormula(D2UnitStrc*,D2UnitStrc*,int,int,int){return hard*8;}', 'int MISSILE_EvaluateMissileFormula(D2UnitStrc*,D2UnitStrc*,int,int,int){return (hard+boulder)*7;}', 1)
    folder = ROOT / 'tools/reference/d2r-named-equipment'
    manifest = json.loads((folder / 'sources.json').read_text())
    pins, tables = {}, {}
    for name in ['skills', 'missiles']:
        raw = (folder / f'json/{name}.json').read_bytes()
        pins[name] = hashlib.sha256(raw).hexdigest()
        assert pins[name] == manifest['files'][f'json/{name}.json']['sha256']
        tables[name] = json.loads(raw)
    skill, field = tables['skills']['249'], tables['missiles']['armageddonfire']
    assert skill['DmgSymPerCalc'] == "skill('Volcano'.blvl)*par6" and skill['Param6'] == 18
    assert skill['EDmgSymPerCalc'] == "(skill('Molten Boulder'.blvl)+skill('Firestorm'.blvl))*par8" and skill['Param8'] == 14
    assert field['EDmgSymPerCalc'] == "(skill('Firestorm'.blvl)+skill('Molten Boulder'.blvl))*7" and not field.get('ApplyMastery')
    assert tables['missiles']['armageddoncontrol']['Skill'] == 'Armageddon'
    assert not any(tables['missiles']['armageddoncontrol'].get(k) for k in ['SrcDamage', 'SrcMissDmg', 'EMin', 'EMax', 'ApplyMastery'])
    base += 'void setup(int kind){skill={};row={};skill.nSrcDam=0;skill.nEType=ELEMTYPE_FIRE;skill.dwELen=0;for(int& v:skill.dwELevLen)v=0;'
    for dest, key in [('dwMinDam', 'MinDam'), ('dwMaxDam', 'MaxDam'), ('dwEMin', 'EMin'), ('dwEMax', 'EMax'), ('nToHitShift', 'HitShift')]:
        base += f'skill.{dest}={skill.get(key,0)};'
    for i in range(5):
        for dest, key in [('dwMinLvlDam', 'MinLevDam'), ('dwMaxLvlDam', 'MaxLevDam'), ('dwEMinLev', 'EMinLev'), ('dwEMaxLev', 'EMaxLev')]:
            base += f'skill.{dest}[{i}]={skill.get(key+str(i+1),0)};'
    base += 'row.nSkill=kind==0?249:0;row.nSrcDamage=row.nSrcMissDmg=0;row.nElemType=ELEMTYPE_FIRE;row.dwElemDmgSymPerCalc=kind==1?1:-1;'
    base += f'if(kind==1){{row.dwElemMin={field["EMin"]};row.dwElemMax={field["EMax"]};row.nHitShift={field["HitShift"]};'
    for i in range(5):
        base += f'row.dwMinElemLev[{i}]={field.get("MinELev"+str(i+1),0)};row.dwMaxElemLev[{i}]={field.get("MaxELev"+str(i+1),0)};'
    base += '}}\n'
    base += r'''
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;bool first=true;printf("[");
for(int kind:{0,1})for(int rank=1;rank<=255;rank++)for(int b:{0,1,20})for(int f:{0,1,20})for(int v:{0,1,20})for(int mastery:{0,50})for(int item:{0,99999}){
owner={};weapon={};missile={};missileData={};setup(kind);weapon.dwUnitType=UNIT_ITEM;missile.dwUnitType=UNIT_MISSILE;missile.dwClassId=kind==0?577:623;missileData.nSkill=249;missileData.nLevel=rank;boulder=b;hard=f;volcano=v;critical=true;critCalls=0;
for(int& stat:owner.stats)stat=item;owner.stats[STAT_PASSIVE_FIRE_MASTERY]=mastery;
D2UnitStrc origin;for(int& stat:origin.stats)stat=99999;D2MissileDamageDataStrc packet;MISSILE_CalculateDamageData(&packet,&owner,&origin,&missile,rank);MISSILE_SetDamageStats(&owner,&missile,&packet,rank);
printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",kind,rank,b,f,v,mastery,item,packet.nMinDamage,packet.nMaxDamage,packet.nFireMinDamage,packet.nFireMaxDamage,packet.nFlags,critCalls,missile.stats[STAT_PASSIVE_FIRE_MASTERY]);first=false;
}printf("]\n");}
'''
    path = STAGE / 'armageddon-damage-source-probe.cpp'
    path.write_text(base)
    binary = path.with_suffix('')
    subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined', '-fno-sanitize-recover=all', str(path), '-o', str(binary)], check=True)
    run = subprocess.run([str(binary)], capture_output=True)
    (STAGE / 'armageddon-damage-sanitizer.log').write_bytes(run.stderr)
    assert run.returncode == 0 and not run.stderr, run.stderr.decode()[-4000:]
    data = json.loads(run.stdout)
    assert len(data) == 55080

    def tier(row, base, growth, rank):
        value, left = row[base], rank - 1
        for i, length in enumerate([7, 8, 6, 6, 2**30], 1):
            count = min(left, length)
            value += count * row.get(growth + str(i), 0)
            left -= count
        return value

    for kind, rank, boulder, firestorm, volcano, mastery, item, pmin, pmax, fmin, fmax, flags, crits, stored in data:
        expected = []
        for maximum in [False, True]:
            value = tier(skill, 'MaxDam' if maximum else 'MinDam', 'MaxLevDam' if maximum else 'MinLevDam', rank)
            value += value * volcano * 18 // 100
            expected.append(value << skill['HitShift'] if kind == 0 else 0)
        for maximum in [False, True]:
            if kind == 0:
                value = tier(skill, 'EMax' if maximum else 'EMin', 'EMaxLev' if maximum else 'EMinLev', rank) << skill['HitShift']
                value += value * (boulder + firestorm) * 14 // 100
                value += value * mastery // 100
            else:
                value = tier(field, 'EMax' if maximum else 'EMin', 'MaxELev' if maximum else 'MinELev', rank)
                value += value * (boulder + firestorm) * 7 // 100
                value <<= field['HitShift']
            expected.append(value)
        assert [pmin, pmax, fmin, fmax, flags, crits, stored] == [*expected, 0, 0, 0], (kind, rank, boulder, firestorm, volcano, mastery, item)
    fixture = STAGE / 'armageddon-damage-source-probe.json'
    fixture.write_text(json.dumps(data, separators=(',', ':')) + '\n')
    evidence = dict(referenceVersion='3.3.93847', legacyCommit=parent['legacyCommit'], handlerEnabled=False, installed=False,
                    sourceHashes=parent['sourceHashes'], routineHashes=parent['routineHashes'], creatorBlockHashes=parent['creatorBlockHashes'],
                    reusedCreatorEvidence='tools/reference/molten-boulder-damage-source-evidence.json', pinnedInputHashes=pins,
                    fixture=dict(path=str(fixture.relative_to(ROOT)), sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(), cases=len(data)),
                    sanitizers='ASan/UBSan exit0 and empty runtime stderr',
                    established=[
                        'All255ranks execute complete source physical/elemental getters, damage creator/add/scale and stat writer across control577 and field623, three independent hard synergy inputs0/1/20, fire mastery0/50 and unrelated99999item-stat controls.',
                        'Control577 captures source-zero skill249 physical and fire endpoints. Physical hard Volcano18percent truncates before HitShift8; fire hard Boulder+Firestorm14percent applies after HitShift8, followed by owner fire mastery once.',
                        'Ground fire623 uses its own missile endpoints, hard Firestorm+Boulder7percent before HitShift2 and no owner mastery. No item weapon/elemental damage, critical inheritance or passive mastery-stat copying is introduced by these source-zero rows.'
                    ], remaining=[
                        'Hard formula/stat/owner/item/ratio and empty monster-type layer copying remain declared inherited services; missile own physical endpoints are declared zero for these blank physical rows. Current executable and complete event/owner/RNG conventions are not certified.',
                        'Actual native endpoint/terminal packet/ground DamageRate41/collision/defense/health/lifecycle/casting/input/timer/reload acceptance remains unimplemented. No Armageddon handler or final asset is enabled.'
                    ], next='Reuse current physical/fire endpoint suppliers only after actual-content comparisons. Compose control25-frame expiry/hit56 area and37-frame Collision1 ground-fire623 with DamageRate41 before native actors.')
    out = ROOT / 'tools/reference/armageddon-damage-source-evidence.json'
    rendered = json.dumps(evidence, indent=2) + '\n'
    if check:
        assert out.read_text() == rendered
    else:
        out.write_text(rendered)
    print(len(data), 'complete Armageddon source damage contexts/all255ranks pass; no handler enabled.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
