#!/usr/bin/env python3
"""Capture complete damage creator/getters/writer for Volcano vent, debris and lava."""
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
    base = base.replace(before, 'int boulder=0,fissure=0,armageddon=0;int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int formula,int,int){return formula==2?boulder*16:(fissure+armageddon)*12;}', 1)
    folder = ROOT / 'tools/reference/d2r-named-equipment'
    manifest = json.loads((folder / 'sources.json').read_text())
    pins, tables = {}, {}
    for name in ['skills', 'missiles']:
        raw = (folder / f'json/{name}.json').read_bytes()
        pins[name] = hashlib.sha256(raw).hexdigest()
        assert pins[name] == manifest['files'][f'json/{name}.json']['sha256']
        tables[name] = json.loads(raw)
    skill = tables['skills']['244']
    assert skill['DmgSymPerCalc'] == "skill('Molten Boulder'.blvl)*par7" and skill['Param7'] == 16
    assert skill['EDmgSymPerCalc'] == "(skill('Eruption'.blvl)+skill('Armageddon'.blvl))*par8" and skill['Param8'] == 12
    for name in ['volcano', 'volcano debris 2', 'volcano small fire']:
        row = tables['missiles'][name]
        assert not any(row.get(k) for k in ['SrcDamage', 'SrcMissDmg', 'EType', 'EMin', 'EMax', 'ApplyMastery'])
        assert row.get('Skill') == (None if name == 'volcano debris 2' else 'Volcano')
    base += 'void setup(int kind){skill={};row={};skill.nSrcDam=0;skill.nEType=ELEMTYPE_FIRE;skill.dwELen=0;for(int& v:skill.dwELevLen)v=0;'
    for dest, key in [('dwMinDam', 'MinDam'), ('dwMaxDam', 'MaxDam'), ('dwEMin', 'EMin'), ('dwEMax', 'EMax'), ('nToHitShift', 'HitShift')]:
        base += f'skill.{dest}={skill.get(key,0)};'
    for i in range(5):
        for dest, key in [('dwMinLvlDam', 'MinLevDam'), ('dwMaxLvlDam', 'MaxLevDam'), ('dwEMinLev', 'EMinLev'), ('dwEMaxLev', 'EMaxLev')]:
            base += f'skill.{dest}[{i}]={skill.get(key+str(i+1),0)};'
    base += 'row.nSkill=kind==1?0:244;row.nSrcDamage=row.nSrcMissDmg=0;row.nElemType=ELEMTYPE_NONE;row.dwElemDmgSymPerCalc=-1;}\n'
    base += r'''
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;bool first=true;printf("[");
for(int kind:{0,1,2})for(int rank=1;rank<=255;rank++)for(int b:{0,1,20})for(int f:{0,1,20})for(int a:{0,1,20})for(int mastery:{0,50})for(int item:{0,99999}){
owner={};weapon={};missile={};missileData={};setup(kind);weapon.dwUnitType=UNIT_ITEM;missile.dwUnitType=UNIT_MISSILE;missile.dwClassId=kind==0?479:kind==1?481:483;missileData.nSkill=244;missileData.nLevel=rank;boulder=b;fissure=f;armageddon=a;critical=true;critCalls=0;
for(int& stat:owner.stats)stat=item;owner.stats[STAT_PASSIVE_FIRE_MASTERY]=mastery;
D2UnitStrc origin;for(int& stat:origin.stats)stat=99999;D2MissileDamageDataStrc packet;MISSILE_CalculateDamageData(&packet,&owner,&origin,&missile,rank);MISSILE_SetDamageStats(&owner,&missile,&packet,rank);
printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",kind,rank,b,f,a,mastery,item,packet.nMinDamage,packet.nMaxDamage,packet.nFireMinDamage,packet.nFireMaxDamage,packet.nFlags,critCalls,missile.stats[STAT_PASSIVE_FIRE_MASTERY]);first=false;
}printf("]\n");}
'''
    path = STAGE / 'volcano-damage-source-probe.cpp'
    path.write_text(base)
    binary = path.with_suffix('')
    subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined', '-fno-sanitize-recover=all', str(path), '-o', str(binary)], check=True)
    run = subprocess.run([str(binary)], capture_output=True)
    (STAGE / 'volcano-damage-sanitizer.log').write_bytes(run.stderr)
    assert run.returncode == 0 and not run.stderr, run.stderr.decode()[-4000:]
    data = json.loads(run.stdout)
    assert len(data) == 82620

    def tier(base, growth, rank):
        value, left = skill[base], rank - 1
        for i, length in enumerate([7, 8, 6, 6, 2**30], 1):
            count = min(left, length)
            value += count * skill.get(growth + str(i), 0)
            left -= count
        return value

    for kind, rank, boulder, fissure, armageddon, mastery, item, pmin, pmax, fmin, fmax, flags, crits, stored in data:
        expected = []
        for maximum in [False, True]:
            value = tier('MaxDam' if maximum else 'MinDam', 'MaxLevDam' if maximum else 'MinLevDam', rank)
            value += value * boulder * 16 // 100
            expected.append(value << skill['HitShift'] if kind != 1 else 0)
        for maximum in [False, True]:
            value = tier('EMax' if maximum else 'EMin', 'EMaxLev' if maximum else 'EMinLev', rank) << skill['HitShift']
            value += value * (fissure + armageddon) * 12 // 100
            value += value * mastery // 100
            expected.append(value if kind != 1 else 0)
        assert [pmin, pmax, fmin, fmax, flags, crits, stored] == [*expected, 0, 0, 0], (kind, rank, boulder, fissure, armageddon, mastery, item)
    fixture = STAGE / 'volcano-damage-source-probe.json'
    fixture.write_text(json.dumps(data, separators=(',', ':')) + '\n')
    evidence = dict(referenceVersion='3.3.93847', legacyCommit=parent['legacyCommit'], handlerEnabled=False, installed=False,
                    sourceHashes=parent['sourceHashes'], routineHashes=parent['routineHashes'], creatorBlockHashes=parent['creatorBlockHashes'],
                    reusedCreatorEvidence='tools/reference/molten-boulder-damage-source-evidence.json', pinnedInputHashes=pins,
                    fixture=dict(path=str(fixture.relative_to(ROOT)), sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(), cases=len(data)),
                    sanitizers='ASan/UBSan exit0 and empty runtime stderr',
                    established=[
                        'All255ranks execute complete source physical/elemental getters, damage creator/add/scale and stat writer with three independent hard synergy inputs0/1/20, fire mastery0/50 and unrelated owner/origin99999item-stat controls.',
                        'Root479 and lava483 both capture source-zero skill244 physical/fire endpoints. Physical hard Molten Boulder16percent truncates before HitShift8; fire hard Fissure+Armageddon12percent applies after HitShift8, followed by one owner fire-mastery application.',
                        'Air debris481 captures zero damage despite retained skill244/rank and damaging origin. No weapon/item elemental damage, critical inheritance or passive mastery-stat copying is introduced by these source-zero routes.'
                    ], remaining=[
                        'Hard formula/stat/owner/item/ratio and empty monster-type layer copying remain declared inherited services. Current executable and complete events/owner/RNG conventions are not certified.',
                        'Native endpoints can reuse existing PhysicalDamage256 and DruidFirestorm.Endpoints. Actual vent/lava collision, damage commitment, hit-delay grouping, saved actors and learned input remain unimplemented.'
                    ], next='Compare actual existing content endpoint suppliers with these source captures; compose saved vent/debris/lava actors with independent lifetimes and modern delay uncertainty explicit.')
    out = ROOT / 'tools/reference/volcano-damage-source-evidence.json'
    rendered = json.dumps(evidence, indent=2) + '\n'
    if check:
        assert out.read_text() == rendered
    else:
        out.write_text(rendered)
    print(len(data), 'complete Volcano source damage contexts/all255ranks pass; no handler enabled.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
