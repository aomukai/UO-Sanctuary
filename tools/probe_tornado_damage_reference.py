#!/usr/bin/env python3
"""Execute complete physical getters/creator/writer with current Tornado data."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills'


def build(check=False):
    subprocess.run([sys.executable, str(ROOT / 'tools/probe_molten_boulder_damage_reference.py'), '--check'], check=True, stdout=subprocess.DEVNULL)
    parent = json.loads((ROOT / 'tools/reference/molten-boulder-damage-source-evidence.json').read_text())
    preflight = json.loads((ROOT / 'tools/reference/tornado-preflight.json').read_text())
    base = (STAGE / 'molten-boulder-damage-source-probe.cpp').read_text().split('void setup(', 1)[0]
    before = 'int volcano=0;int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int formula,int,int){return formula==2?volcano*12:hard*8;}'
    assert base.count(before) == 1
    base = base.replace(before, 'int twister=0,hurricane=0;int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int,int,int){return (hard+twister+hurricane)*9;}', 1)
    folder = ROOT / 'tools/reference/d2r-named-equipment'
    tables = {}
    for name in ['skills', 'missiles']:
        raw = (folder / f'json/{name}.json').read_bytes()
        assert hashlib.sha256(raw).hexdigest() == preflight['pinnedInputHashes'][name]
        tables[name] = json.loads(raw)
    skill, row = tables['skills']['245'], tables['missiles']['tornado']
    assert skill['Param8'] == 9 and skill['DmgSymPerCalc'] == "(skill('Cyclone Armor'.blvl)+skill('Twister'.blvl)+skill('Hurricane'.blvl))*par8"
    assert row['Skill'] == 'Tornado' and not any(row.get(k) for k in ['SrcDamage', 'SrcMissDmg', 'EType', 'EMin', 'EMax', 'ApplyMastery'])
    base += 'void setup(){skill={};row={};skill.nSrcDam=0;skill.nEType=ELEMTYPE_NONE;skill.dwELen=0;for(int& v:skill.dwELevLen)v=0;'
    for dest, key in [('dwMinDam', 'MinDam'), ('dwMaxDam', 'MaxDam'), ('dwEMin', 'EMin'), ('dwEMax', 'EMax'), ('nToHitShift', 'HitShift')]:
        base += f'skill.{dest}={skill.get(key,0)};'
    for i in range(5):
        for dest, key in [('dwMinLvlDam', 'MinLevDam'), ('dwMaxLvlDam', 'MaxLevDam'), ('dwEMinLev', 'EMinLev'), ('dwEMaxLev', 'EMaxLev')]:
            base += f'skill.{dest}[{i}]={skill.get(key+str(i+1),0)};'
    base += 'row.nSkill=245;row.nSrcDamage=row.nSrcMissDmg=0;row.nElemType=ELEMTYPE_NONE;row.dwElemDmgSymPerCalc=-1;}\n'
    base += r'''
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;bool first=true;printf("[");
for(int rank=1;rank<=255;rank++)for(int c:{0,1,20})for(int t:{0,1,20})for(int h:{0,1,20})for(int mastery:{0,50})for(int item:{0,99999}){
owner={};weapon={};missile={};missileData={};setup();weapon.dwUnitType=UNIT_ITEM;missile.dwUnitType=UNIT_MISSILE;missile.dwClassId=478;missileData.nSkill=245;missileData.nLevel=rank;hard=c;twister=t;hurricane=h;critical=true;critCalls=0;
for(int& stat:owner.stats)stat=item;owner.stats[STAT_PASSIVE_FIRE_MASTERY]=mastery;
D2UnitStrc origin;for(int& stat:origin.stats)stat=99999;D2MissileDamageDataStrc packet;MISSILE_CalculateDamageData(&packet,&owner,&origin,&missile,rank);MISSILE_SetDamageStats(&owner,&missile,&packet,rank);
printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",rank,c,t,h,mastery,item,packet.nMinDamage,packet.nMaxDamage,packet.nFireMinDamage,packet.nFireMaxDamage,packet.nFlags,critCalls,missile.stats[STAT_PASSIVE_FIRE_MASTERY]);first=false;
}printf("]\n");}
'''
    path = STAGE / 'tornado-damage-source-probe.cpp'
    path.write_text(base)
    binary = path.with_suffix('')
    subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined', '-fno-sanitize-recover=all', str(path), '-o', str(binary)], check=True)
    run = subprocess.run([str(binary)], capture_output=True)
    (STAGE / 'tornado-damage-sanitizer.log').write_bytes(run.stderr)
    assert run.returncode == 0 and not run.stderr, run.stderr.decode()[-4000:]
    data = json.loads(run.stdout)
    assert len(data) == 27540

    def tier(maximum, rank):
        prefix = 'Max' if maximum else 'Min'
        value, left = skill[prefix + 'Dam'], rank - 1
        for i, length in enumerate([7, 8, 6, 6, 2**30], 1):
            count = min(left, length)
            value += count * skill.get(prefix + 'LevDam' + str(i), 0)
            left -= count
        return value

    for rank, cyclone, twister, hurricane, mastery, item, pmin, pmax, fmin, fmax, flags, crits, stored in data:
        expected = []
        for maximum in [False, True]:
            value = tier(maximum, rank)
            value += value * (cyclone + twister + hurricane) * 9 // 100
            expected.append(value << skill['HitShift'])
        assert [pmin, pmax, fmin, fmax, flags, crits, stored] == [*expected, 0, 0, 0, 0, 0], (rank, cyclone, twister, hurricane, mastery, item)
    fixture = STAGE / 'tornado-damage-source-probe.json'
    fixture.write_text(json.dumps(data, separators=(',', ':')) + '\n')
    evidence = dict(referenceVersion='3.3.93847', legacyCommit=parent['legacyCommit'], handlerEnabled=False, installed=False,
                    pinnedInputHashes=preflight['pinnedInputHashes'], sourceHashes=parent['sourceHashes'], routineHashes=parent['routineHashes'],
                    creatorBlockHashes=parent['creatorBlockHashes'], reusedCreatorEvidence='tools/reference/molten-boulder-damage-source-evidence.json',
                    fixture=dict(path=str(fixture.relative_to(ROOT)), sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(), cases=len(data)),
                    sanitizers='ASan/UBSan exit0 and empty runtime stderr',
                    established=[
                        'All255ranks execute complete physical/elemental getters, damage creator/add/scale and stat writer with current skill245/missile478. Independent hard Cyclone Armor/Twister/Hurricane0/1/20 controls supply9percent each.',
                        'Physical synergy truncates before HitShift8. Elemental endpoints remain zero. Unrelated99999owner/origin item stats, fire mastery0/50 and enabled critical controls do not introduce item damage, critical inheritance or copied mastery stats.'
                    ], remaining=[
                        'Formula/stat/ratio/owner/item and empty monster-type layer copying remain declared inherited services. Modern executable and complete event/stat/seed conventions are not certified.',
                        'Actual native endpoint comparison, periodic/direct health delivery, charged path/final seed, modern overlap, learned input and saved actors require separate acceptance.'
                    ], next='Compare the existing actual PhysicalDamage256 supplier, then compose75-frame charged launch/path and distinct periodic-area/direct consumers after Volcano installation.')
    out = ROOT / 'tools/reference/tornado-damage-source-evidence.json'
    text = json.dumps(evidence, indent=2) + '\n'
    if check:
        assert out.read_text() == text
    else:
        out.write_text(text)
    print('Tornado:27540complete source damage contexts/all255ranks pass; no handler enabled.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
