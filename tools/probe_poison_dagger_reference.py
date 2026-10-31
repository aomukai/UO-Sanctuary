#!/usr/bin/env python3
"""Compile Poison Dagger's complete elemental roll and shared poison lane."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/poison-dagger-source'
sha = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()


def build(check=False):
    tree = json.loads((ROOT / '.local/class-skills/d2moo-source-tree.json').read_text())
    assert tree['sha'] == '0c0bbd1e6bef9181a3da1c046523af622a925416'
    hashes, blocks = {}, {}

    def source(name):
        path = ROOT / '.local/class-skills/d2moo-pinned-source' / name
        raw = path.read_bytes()
        expected = next(r['sha'] for r in tree['tree'] if r['path'] == name)
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == expected
        hashes[name] = sha(path)
        return raw.decode()

    def routine(path, name):
        text = source(path)
        match = re.search(r'^\w[^;\n]*\b' + name + r'\([^;{]*?\)\s*\n\{', text, re.M)
        assert match, name
        end = text.index('{', match.start()) + 1
        depth = 1
        while depth:
            depth += (text[end] == '{') - (text[end] == '}')
            end += 1
        body = text[match.start():end]
        blocks[name] = hashlib.sha256(body.encode()).hexdigest()
        return body

    common = 'source/D2Common/src/D2Skills.cpp'
    game = 'source/D2Game/src/SKILLS/Skills.cpp'
    names = ['SKILLS_CalculateDamageBonusByLevel', 'SKILLS_CalculateMasteryBonus',
             'SKILLS_GetMinElemDamage', 'SKILLS_GetMaxElemDamage', 'SKILLS_GetElementalLength']
    bodies = [routine(common, n) for n in names]
    bodies += [routine('source/D2Game/src/MONSTER/MonsterUnique.cpp', 'MONSTERUNIQUE_CalculatePercentage'),
               routine(game, 'sub_6FD11E40'), routine(game, 'D2GAME_RollElementalDamage_6FD14DD0')]
    damage = source('source/D2Game/src/UNIT/SUnitDmg.cpp')
    start = damage.index('\tint32_t nMinDamage = STATLIST_UnitGetStatValue(pAttacker, STAT_POISONMINDAM, 0);')
    end = damage.index('\n\tif (pDamage->dwColdDamage > 0)', start)
    lane = damage[start:end]
    prior = json.loads((ROOT / 'tools/reference/venom-damage-engine-evidence.json').read_text())
    assert hashlib.sha256(lane.encode()).hexdigest() == prior['compiledUnchangedStatementBlock']['sha256']
    for name in names + ['MONSTERUNIQUE_CalculatePercentage']:
        assert blocks[name] == prior['compiledUnchangedRoutines'][name]
    fields = ROOT / 'tools/reference/d2r-named-equipment/json/skills.json'
    pins = json.loads((fields.parent.parent / 'sources.json').read_text())
    assert sha(fields) == pins['files']['json/skills.json']['sha256']
    row = json.loads(fields.read_text())['73']
    assert row['srvstfunc'] == 16 and row['srvdofunc'] == 32 and row['SrcDam'] == 128
    assert row['EDmgSymPerCalc'] == "(skill('Poison Explosion'.blvl)+skill('Poison Nova'.blvl))*par8"
    stats = sorted(set(re.findall(r'\bSTAT_\w+', '\n'.join(bodies) + lane)))
    elements = sorted(set(re.findall(r'\bELEMTYPE_\w+', '\n'.join(bodies))))
    members = sorted(set(re.findall(r'pDamage->(\w+)', '\n'.join(bodies) + lane)))
    cpp = r'''#include <algorithm>
#include <cassert>
#include <cstdint>
#include <cstdio>
#include <initializer_list>
#define __fastcall
#define __stdcall
#define D2_MAYBE_UNUSED(x) (void)(x)
using BOOL=int;
'''
    cpp += 'enum Stats{' + ','.join(stats) + '};\nenum Elements{' + ','.join(elements) + '};\n'
    cpp += 'struct D2DamageStrc{' + ''.join('int ' + m + '=0;' for m in members) + '};\n'
    cpp += r'''struct D2UnitStrc{int pSeed=0;int stats[128]={};}owner;
struct D2SkillsTxt{int nEType=0,nToHitShift=0,dwEMin=0,dwEMax=0,dwEMinLev[5]={},dwEMaxLev[5]={},dwEDmgSymPerCalc=1,dwELen=0,dwELevLen[3]={},dwELenSymPerCalc=-1;}skill;
int hard=0,rollMode=0,rollBound=-1;
D2SkillsTxt* DATATBLS_GetSkillsTxtRecord(int id){assert(id==73);return &skill;}
D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int id){return DATATBLS_GetSkillsTxtRecord(id);}
int STATLIST_UnitGetStatValue(D2UnitStrc* unit,int stat,int){return unit->stats[stat];}
int DATATBLS_ApplyRatio(int value,int percent,int divisor){return (int)((int64_t)value*percent/divisor);}
int ITEMS_RollLimitedRandomNumber(int*,int span){assert(span>=0);rollBound=span;return rollMode&&span>0?span-1:0;}
int ITEMS_RollRandomNumber(int*){assert(false);return 0;}
'''
    cpp += f'int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int,int,int){{return hard*{row["Param8"]};}}\n'
    cpp += '\n'.join(bodies) + '\nvoid MeleePoisonLane(D2UnitStrc* pAttacker,D2DamageStrc* pDamage,int nSrcDam){\n' + lane + '\n}\n'
    setup = f'skill.nEType=ELEMTYPE_POIS;skill.nToHitShift={row["HitShift"]};'
    setup += f'skill.dwEMin={row["EMin"]};skill.dwEMax={row["EMax"]};skill.dwELen={row["ELen"]};'
    for i in range(5):
        setup += f'skill.dwEMinLev[{i}]={row["EMinLev"+str(i+1)]};skill.dwEMaxLev[{i}]={row["EMaxLev"+str(i+1)]};'
    for i in range(3):
        setup += f'skill.dwELevLen[{i}]={row["ELevLen"+str(i+1)]};'
    cpp += r'''int main(){bool first=true;std::printf("[");
for(int rank:{1,2,3,8,9,16,17,22,23,28,29,255})for(int points:{0,1,40})for(int mastery:{0,25})
for(int item:{0,102})for(int count:{0,1,3})for(int length:{0,200})for(int over:{0,10})for(int highSkill:{0,1})for(int highItem:{0,1}){
owner={};skill={};hard=points;
''' + setup + r'''
owner.stats[STAT_PASSIVE_POIS_MASTERY]=mastery;
owner.stats[STAT_POISONMINDAM]=item;owner.stats[STAT_POISONMAXDAM]=item?item+19:0;
owner.stats[STAT_POISONLENGTH]=length;owner.stats[STAT_POISON_COUNT]=count;owner.stats[STAT_SKILL_POISON_OVERRIDE_LENGTH]=over;
int minimum=SKILLS_GetMinElemDamage(&owner,73,rank,1),maximum=SKILLS_GetMaxElemDamage(&owner,73,rank,1),frames=SKILLS_GetElementalLength(&owner,73,rank,1);
rollMode=highSkill;rollBound=-1;D2DamageStrc damage;int rolled=D2GAME_RollElementalDamage_6FD14DD0(&owner,&damage,73,rank);int skillBound=rollBound;
assert(damage.dwPoisDamage==rolled&&damage.dwPoisLen==frames&&damage.dwHitClass==0x50);
rollMode=highItem;rollBound=-1;MeleePoisonLane(&owner,&damage,128);
if(!first)std::printf(",");first=false;
std::printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",rank,points,mastery,item,item?item+19:0,length,count,over,highSkill,highItem,minimum,maximum,frames,rolled,skillBound,rollBound,damage.dwPoisDamage,damage.dwPoisLen);
}std::printf("]\n");}
'''
    STAGE.mkdir(parents=True, exist_ok=True)
    path = STAGE / 'source.cpp'
    path.write_text(cpp)
    compile_result = subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined',
        '-fno-omit-frame-pointer', str(path), '-o', str(STAGE / 'probe')], capture_output=True, text=True)
    (STAGE / 'compiler.log').write_text(compile_result.stdout + compile_result.stderr)
    assert compile_result.returncode == 0, compile_result.stderr
    run = subprocess.run([str(STAGE / 'probe')], capture_output=True)
    (STAGE / 'sanitizer.log').write_bytes(run.stderr)
    assert run.returncode == 0 and not run.stderr, run.stderr
    cases = json.loads(run.stdout)
    assert len(cases) == 6912
    # Independent table arithmetic checks the driver and output shape; native
    # consumers must separately compare these complete-source outputs.
    for r in cases:
        rank, points, mastery, low, high, length, count, over, skill_high, item_high = r[:10]
        endpoints = []
        for key in ['EMin', 'EMax']:
            value, extra = row[key], rank - 1
            for i, size in enumerate([7, 8, 6, 6, 2**30], 1):
                used = min(extra, size); value += used * row[key+'Lev'+str(i)]; extra -= used
            value <<= row['HitShift']; value += value * points * row['Param8'] // 100
            value += value * mastery // 100; endpoints.append(value)
        frames = row['ELen'] + (rank - 1) * row['ELevLen1']
        rolled = endpoints[0] + (max(0, endpoints[1]-endpoints[0]-1) if skill_high else 0)
        item_low, item_high_value = low + low * mastery // 100, high + high * mastery // 100
        item_bound = item_high_value - item_low if high > 0 else -1
        rate = rolled + (item_low + (max(0, item_bound-1) if item_high else 0) if high > 0 else 0)
        duration = frames + (over if over > 0 else length)
        if over <= 0 and count > 1: duration //= count
        assert r[10:] == [*endpoints, frames, rolled, endpoints[1]-endpoints[0], item_bound, rate, duration], r
    (STAGE / 'cases.json').write_text(json.dumps(cases, separators=(',', ':')) + '\n')
    result = dict(referenceVersion='3.3.93847', talentId='necromancer.poisondagger',
        legacyCommit=tree['sha'], cases=6912, sourceHashes=hashes, compiledUnchangedRoutines=blocks,
        unchangedPoisonLaneSha256=hashlib.sha256(lane.encode()).hexdigest(), pinnedSkillsSha256=sha(fields),
        established=[
            'Complete elemental getters and roll/element assignment execute before the unchanged shared melee poison lane. '
            'Skill and owner-item rates join one packet. Source128 retains that sum; lengths add before item-count averaging.',
            'Poison mastery applies within the complete skill endpoint getter and separately to item endpoints. '
            'A positive override adds its length to the existing skill length and suppresses item-count division.',
            'All damage tier boundaries, effective rank255, declared hard synergy totals0/1/40, mastery0/25, '
            'item endpoints/count/length/override and separate exclusive roll endpoints are exercised.'],
        limits=[
            'Formula, prepared stats, tables and deterministic RNG endpoints are declared services. '
            'Synthetic item count/length combinations test arithmetic and do not claim valid equipment projections.',
            'Only the complete poison lane of FillDamageValues executes, not its physical, drain, event or allocation paths. '
            'The skill start/contact/weapon eligibility/mode and modern executable remain separate acceptance requirements.',
            'No native runtime or learned skill behavior changes in this source-probe increment.'],
        artifacts={str(p.relative_to(ROOT)):sha(p) for p in [Path(__file__).resolve(), path,
            STAGE/'cases.json', STAGE/'compiler.log', STAGE/'sanitizer.log']})
    output = ROOT / 'tools/reference/poison-dagger-source-evidence.json'
    rendered = json.dumps(result, indent=2) + '\n'
    if check: assert output.read_text() == rendered
    else: output.write_text(rendered)
    print('Poison Dagger:6912complete elemental-roll/shared-poison-lane contexts;sanitizers clean;native delivery pending.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
