#!/usr/bin/env python3
"""Run complete source endpoint getters/creator/writer with current Fissure data."""
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
    before = 'return formula==2?volcano*12:hard*8;'
    assert base.count(before) == 1
    base = base.replace(before, 'return (hard+volcano)*12;', 1)
    directory = ROOT / 'tools/reference/d2r-named-equipment'
    manifest = json.loads((directory / 'sources.json').read_text())
    pins, tables = {}, {}
    for name in ['skills', 'missiles']:
        raw = (directory / f'json/{name}.json').read_bytes()
        pins[name] = hashlib.sha256(raw).hexdigest()
        assert pins[name] == manifest['files'][f'json/{name}.json']['sha256']
        tables[name] = json.loads(raw)
    skill = tables['skills']['234']
    assert skill['skill'] == 'Eruption' and skill['EDmgSymPerCalc'] == "(skill('Firestorm'.blvl)+skill('Volcano'.blvl))*par8" and skill['Param8'] == 12
    for name in ['erruption center', 'erruption crack 1', 'erruption crack 2']:
        row = tables['missiles'][name]
        assert not any(row.get(k) for k in ['SrcDamage', 'SrcMissDmg', 'EType', 'EMin', 'EMax', 'ApplyMastery', 'MissileSkill'])
        assert row.get('Skill', 0) == (0 if name == 'erruption center' else 'Eruption')
    base += 'void setup(int kind){skill={};row={};skill.nSrcDam=0;skill.nEType=ELEMTYPE_FIRE;skill.dwELen=0;for(int& v:skill.dwELevLen)v=0;'
    for dest, key in [('dwMinDam', 'MinDam'), ('dwMaxDam', 'MaxDam'), ('dwEMin', 'EMin'), ('dwEMax', 'EMax'), ('nToHitShift', 'HitShift')]:
        base += f'skill.{dest}={skill.get(key, 0)};'
    for i in range(5):
        for dest, key in [('dwMinLvlDam', 'MinLevDam'), ('dwMaxLvlDam', 'MaxLevDam'), ('dwEMinLev', 'EMinLev'), ('dwEMaxLev', 'EMaxLev')]:
            base += f'skill.{dest}[{i}]={skill.get(key+str(i+1),0)};'
    base += 'row.nSkill=kind==0?0:234;row.nSrcDamage=row.nSrcMissDmg=0;row.nElemType=ELEMTYPE_NONE;row.dwElemDmgSymPerCalc=-1;}\n'
    base += r'''
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;bool first=true;printf("[");
for(int kind:{0,1,2})for(int rank=1;rank<=255;rank++)for(int points:{0,1,20})for(int volcanic:{0,1,20})for(int mastery:{0,50})for(int item:{0,99999}){
 owner={};weapon={};missile={};missileData={};setup(kind);weapon.dwUnitType=UNIT_ITEM;missile.dwUnitType=UNIT_MISSILE;missile.dwClassId=461+kind;missileData.nSkill=234;missileData.nLevel=rank;hard=points;volcano=volcanic;critical=true;critCalls=0;
 for(int& stat:owner.stats)stat=item;owner.stats[STAT_PASSIVE_FIRE_MASTERY]=mastery;
 D2UnitStrc origin;for(int& stat:origin.stats)stat=99999;D2MissileDamageDataStrc packet;MISSILE_CalculateDamageData(&packet,&owner,&origin,&missile,rank);MISSILE_SetDamageStats(&owner,&missile,&packet,rank);
 printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",kind,rank,points,volcanic,mastery,item,packet.nMinDamage,packet.nMaxDamage,packet.nFireMinDamage,packet.nFireMaxDamage,packet.nFlags,critCalls,missile.stats[STAT_PASSIVE_FIRE_MASTERY]);first=false;
}printf("]\n");}
'''
    path = STAGE / 'fissure-damage-source-probe.cpp'
    path.write_text(base)
    binary = path.with_suffix('')
    subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined', '-fno-sanitize-recover=all', str(path), '-o', str(binary)], check=True)
    run = subprocess.run([str(binary)], capture_output=True)
    (STAGE / 'fissure-damage-sanitizer.log').write_bytes(run.stderr)
    assert run.returncode == 0 and not run.stderr, run.stderr.decode()[-4000:]
    data = json.loads(run.stdout)
    assert len(data) == 27540

    def tier(maximum, rank):
        key = 'EMax' if maximum else 'EMin'
        value, left = skill[key], rank - 1
        for i, length in enumerate([7, 8, 6, 6, 2**30], 1):
            count = min(left, length)
            value += count * skill.get(key + 'Lev' + str(i), 0)
            left -= count
        return value

    for kind, rank, hard, volcano, mastery, item, pmin, pmax, fmin, fmax, flags, crits, stored in data:
        expected = []
        for maximum in [False, True]:
            value = tier(maximum, rank) << 8
            value += value * (hard + volcano) * 12 // 100
            value += value * mastery // 100
            expected.append(value if kind != 0 else 0)
        assert [pmin, pmax, fmin, fmax, flags, crits, stored] == [0, 0, *expected, 0, 0, 0], (kind, rank, hard, volcano, mastery, item)
    fixture = STAGE / 'fissure-damage-source-probe.json'
    fixture.write_text(json.dumps(data, separators=(',', ':')) + '\n')
    evidence = dict(referenceVersion='3.3.93847', handlerEnabled=False, installed=False,
                    legacyCommit=parent['legacyCommit'], sourceHashes=parent['sourceHashes'], routineHashes=parent['routineHashes'],
                    reusedCreatorEvidence='tools/reference/molten-boulder-damage-source-evidence.json',
                    creatorBlockHashes=parent['creatorBlockHashes'], pinnedInputHashes=pins,
                    fixture=dict(path=str(fixture.relative_to(ROOT)), sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(), cases=len(data)),
                    sanitizers='ASan/UBSan exit0 and empty runtime stderr',
                    established=[
                        'All255ranks use complete physical/elemental getters, missile elemental getters, creator/add/scale and setter bodies with current center461/crack462/463 rows.',
                        'Center461 has zero endpoints even while carrying skill234. Both crack variants use skill234 fire endpoints, HitShift8, then hard Firestorm+Volcano12percent synergy, then owner fire mastery once. Physical endpoints remain zero.',
                        'Zero/one/twenty independent hard points, separate owner mastery and unrelated99999owner/origin item-stat controls preserve no weapon/elemental item inheritance or critical calls, and no copied owner mastery stat.'
                    ], limits=parent['limits'] + [
                        'Missile-owned physical getters and elemental length are declared zero services for the pinned center zero-damage row. Complete skill-owned physical getters and missile/skill elemental getters execute; no nonzero center endpoint is inferred.',
                        'Formula evaluation is the explicit current(hardFirestorm+hardVolcano)*12service; current formula interpreter is checked separately against these captures.',
                        'These are fresh creation endpoints. Native crack creation must supply current hard/mastery stats at retained cast rank; hit rolls, immunity, damage commitment and modern overlap identity are not established here.'
                    ], next='Compare actual content SkillValues and the reusable fire endpoint supplier; connect current crack damage/collision/lifetime to the saved native emitter.')
    out = ROOT / 'tools/reference/fissure-damage-source-evidence.json'
    text = json.dumps(evidence, indent=2) + '\n'
    if check:
        assert out.read_text() == text
    else:
        out.write_text(text)
    print(len(data), 'complete Fissure damage creator/getter/writer contexts pass; no handler enabled.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
