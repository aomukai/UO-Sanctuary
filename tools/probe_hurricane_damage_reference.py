#!/usr/bin/env python3
"""Compose Hurricane's full source elemental getters, length, roll and packet map."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills'


def build(check=False):
    subprocess.run([sys.executable, str(ROOT / 'tools/probe_molten_boulder_damage_reference.py'), '--check'], check=True, stdout=subprocess.DEVNULL)
    parent = json.loads((ROOT / 'tools/reference/molten-boulder-damage-source-evidence.json').read_text())
    preflight = json.loads((ROOT / 'tools/reference/druid-weather-preflight.json').read_text())
    tree = json.loads((STAGE / 'd2moo-source-tree.json').read_text())
    hashes, routines = {}, {}

    def routine(relative, name):
        raw = (STAGE / 'd2moo-pinned-source' / relative).read_bytes()
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(x['sha'] for x in tree['tree'] if x['path'] == relative)
        hashes[relative] = hashlib.sha256(raw).hexdigest()
        source = raw.decode()
        match = re.search(r'^.*\b' + re.escape(name) + r'\([^;\n]*\)\s*\n\{', source, re.M)
        assert match, name
        end, depth = source.index('{', match.start()) + 1, 1
        while depth:
            depth += (source[end] == '{') - (source[end] == '}')
            end += 1
        body = source[match.start():end]
        routines[name] = hashlib.sha256(body.encode()).hexdigest()
        return body + '\n'

    cpp = (STAGE / 'molten-boulder-damage-source-probe.cpp').read_text().split('void setup(', 1)[0]
    before = 'int volcano=0;int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int formula,int,int){return formula==2?volcano*12:hard*8;}'
    assert cpp.count(before) == 1
    cpp = cpp.replace(before, 'int tornado=0;int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int formula,int,int){return formula==2?0:(hard+tornado)*9;}', 1)
    cpp = cpp.replace('struct D2UnitStrc{int dwUnitType', 'struct D2UnitStrc{uint32_t pSeed=1;int dwUnitType', 1)
    # Verify inherited complete getters, rather than accepting declarations or rewritten math.
    for name in ['SKILLS_GetMinPhysDamage', 'SKILLS_GetMaxPhysDamage', 'SKILLS_GetMinElemDamage', 'SKILLS_GetMaxElemDamage', 'SKILLS_GetElementalLength', 'SKILLS_CalculateMasteryBonus']:
        body = routine('source/D2Common/src/D2Skills.cpp', name)
        assert cpp.count(body.rstrip()) == 1
    cpp += r'''
#include <cassert>
constexpr int ELEMTYPE_RAND=99,STAT_FIRERESIST=101,STAT_LIGHTRESIST=102,STAT_MAGICRESIST=103,STAT_COLDRESIST=104,STAT_POISONRESIST=105;
struct D2DamageStrc{int dwPhysDamage=0,dwFireDamage=0,dwLtngDamage=0,dwMagDamage=0,dwColdDamage=0,dwPoisDamage=0,dwLifeLeech=0,dwManaLeech=0,dwStamLeech=0,dwBurnDamage=0,dwHitClass=0,dwColdLen=0,dwPoisLen=0,dwStunLen=0,dwBurnLen=0,dwFrzLen=0;};
int edge=0,calls=0,span=-1;bool hasRow=true;
D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int id){assert(id==250);return hasRow?&skill:nullptr;}
int ITEMS_RollLimitedRandomNumber(uint32_t* seed,int n){assert(seed==&owner.pSeed&&n>=0);calls++;span=n;return edge&&n>0?n-1:0;}
uint32_t ITEMS_RollRandomNumber(uint32_t*){std::abort();}
'''
    for name in ['sub_6FD11E40', 'D2GAME_RollElementalDamage_6FD14DD0', 'D2GAME_RollPhysicalDamage_6FD14EC0']:
        cpp += routine('source/D2Game/src/SKILLS/Skills.cpp', name)
    skill = preflight['skills']['Hurricane']
    assert skill['EDmgSymPerCalc'] == "(skill('Twister'.blvl)+skill('Tornado'.blvl))*par8" and skill['ELen'] == 50
    cpp += 'void setup(){skill={};skill.nSrcDam=0;skill.nEType=ELEMTYPE_COLD;skill.dwMinDam=skill.dwMaxDam=0;for(int& v:skill.dwMinLvlDam)v=0;for(int& v:skill.dwMaxLvlDam)v=0;skill.dwELen=50;for(int& v:skill.dwELevLen)v=0;skill.dwELenSymPerCalc=-1;'
    for dest, key in [('dwEMin', 'EMin'), ('dwEMax', 'EMax'), ('nToHitShift', 'HitShift')]:
        cpp += f'skill.{dest}={skill[key]};'
    for i in range(5):
        cpp += f'skill.dwEMinLev[{i}]={skill.get("EMinLev"+str(i+1),0)};skill.dwEMaxLev[{i}]={skill.get("EMaxLev"+str(i+1),0)};'
    cpp += '}\n'
    cpp += r'''
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;bool first=true;printf("[");
for(int rank=1;rank<=255;rank++)for(int t:{0,1,20})for(int n:{0,1,20})for(int mastery:{0,50,200})for(int item:{0,99999})for(int roll:{0,1}){
 owner={};setup();hard=t;tornado=n;edge=roll;hasRow=true;
 for(int& stat:owner.stats)stat=item;owner.stats[STAT_PASSIVE_COLD_MASTERY]=mastery;
 int low=SKILLS_GetMinElemDamage(&owner,250,rank,1),high=SKILLS_GetMaxElemDamage(&owner,250,rank,1),length=SKILLS_GetElementalLength(&owner,250,rank,1);
 D2DamageStrc packet;calls=0;span=-1;int value=D2GAME_RollElementalDamage_6FD14DD0(&owner,&packet,250,rank);
 assert(packet.dwFireDamage==0&&packet.dwPoisDamage==0&&packet.dwMagDamage==0&&packet.dwLtngDamage==0&&packet.dwPhysDamage==0);
 printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",rank,t,n,mastery,item,roll,low,high,length,value,packet.dwColdDamage,packet.dwColdLen,packet.dwHitClass,calls,span);first=false;
 D2GAME_RollPhysicalDamage_6FD14EC0(&owner,&packet,250,rank);assert(packet.dwPhysDamage==0);
}
hasRow=false;D2DamageStrc empty;calls=0;assert(D2GAME_RollElementalDamage_6FD14DD0(&owner,&empty,250,1)==0&&calls==0);printf("]\n");}
'''
    path = STAGE / 'hurricane-damage-source-probe.cpp'
    path.write_text(cpp)
    binary = path.with_suffix('')
    subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined', '-fno-sanitize-recover=all', str(path), '-o', str(binary)], check=True)
    run = subprocess.run([str(binary)], capture_output=True)
    (STAGE / 'hurricane-damage-sanitizer.log').write_bytes(run.stderr)
    assert run.returncode == 0 and not run.stderr, run.stderr.decode()[-4000:]
    data = json.loads(run.stdout)
    assert len(data) == 27540

    def endpoint(maximum, rank):
        prefix = 'EMax' if maximum else 'EMin'
        value, left = skill[prefix], rank-1
        for i, length in enumerate([7, 8, 6, 6, 2**30], 1):
            count = min(left, length)
            value += count*skill.get(prefix+'Lev'+str(i), 0)
            left -= count
        return value << skill['HitShift']

    for rank, twister, tornado, mastery, item, edge, low, high, length, value, cold, cold_length, hit, calls, span in data:
        expected = []
        for maximum in [False, True]:
            v = endpoint(maximum, rank)
            v += v*(twister+tornado)*9//100
            v += v*mastery//100
            expected.append(v)
        roll = expected[0] + (expected[1]-expected[0]-1 if edge else 0)
        assert [low, high, length, value, cold, cold_length, hit, calls, span] == [*expected, 50, roll, roll, 50, 48, 1, expected[1]-expected[0]], (rank, twister, tornado, mastery, edge)
    fixture = STAGE / 'hurricane-damage-source-probe.json'
    fixture.write_text(json.dumps(data, separators=(',', ':')) + '\n')
    evidence = dict(referenceVersion='3.3.93847', handlerEnabled=False, installed=False, legacyCommit=tree['sha'],
                    pinnedInputHashes=preflight['pinnedInputHashes'], sourceHashes=hashes, compiledUnchangedRoutines=routines,
                    inheritedDamageEvidence='tools/reference/molten-boulder-damage-source-evidence.json',
                    fixture=dict(path=str(fixture.relative_to(ROOT)), sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(), cases=len(data)),
                    sanitizers='ASan/UBSan exit0 and empty runtime stderr',
                    established=[
                        'Complete elemental/physical getters, mastery, elemental length, both roll wrappers and elemental packet mapper execute all255ranks with independent hard Twister/Tornado0/1/20, declared mastery0/50/200 and unrelated item-stat controls.',
                        'Cold endpoints apply9percent per hard synergy after HitShift8, then declared outgoing cold mastery once. The roll is max-exclusive; both endpoint RNG controls execute. Each packet writes cold damage, length50 and hitclass0x30; physical and other elemental channels remain zero.',
                        'Source roll APIs read current owner stat/formula services at event time; nothing here introduces item weapon damage, captured cast-time endpoints or per-target rerolls. A missing skill record makes no elemental RNG call.'
                    ], services=[
                        'Hard-skill formula lookup, prepared stats/item metadata and ratio services are inherited bounded source-fixture services. Declared outgoing cold mastery controls do not certify real gear or the distinct cold resistance pierce producer.',
                        'RNG is an explicit first/last-valid-output service, not the full source seed stream. Per-target packet copies, resistance/MDR/absorb/length difficulty/cold state and health are separate consumers.',
                        'Actual native SkillValues/interpreter/endpoints, weather lifecycle/casting/input/timers/reloads, modern executable/client and final effects/icons remain unimplemented or unverified; no handler or class certificate is enabled.'
                    ], next='Compare current SkillValues elemental endpoints/duration against these source vectors, then implement saved Hurricane and live-stat periodic area after Tornado installation.')
    out = ROOT / 'tools/reference/hurricane-damage-source-evidence.json'
    text = json.dumps(evidence, indent=2) + '\n'
    if check:
        assert out.read_text() == text
    else:
        out.write_text(text)
    print('Hurricane:27540complete source endpoint/length/roll/packet cases pass; no handler enabled.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
