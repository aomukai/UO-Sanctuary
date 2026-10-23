#!/usr/bin/env python3
"""Compile unchanged supplementary Charged Strike start, emission and bolt init."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills'


def build(check=False):
    tree = json.loads((STAGE / 'd2moo-source-tree.json').read_text())
    assert tree['sha'] == '0c0bbd1e6bef9181a3da1c046523af622a925416'
    hashes, blocks = {}, {}

    def source(path):
        raw = (STAGE / 'd2moo-pinned-source' / path).read_bytes()
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(
            x['sha'] for x in tree['tree'] if x['path'] == path)
        hashes[path] = hashlib.sha256(raw).hexdigest()
        return raw.decode()

    def routine(text, name):
        match = re.search(r'^.*\b' + name + r'\([^;\n]*\)\s*\n\{', text, re.M)
        assert match, name
        end, depth = text.index('{', match.start()) + 1, 1
        while depth:
            depth += (text[end] == '{') - (text[end] == '}')
            end += 1
        blocks[name] = text[match.start():end]
        return blocks[name] + '\n'

    ama = source('source/D2Game/src/SKILLS/SkillAma.cpp')
    sor = source('source/D2Game/src/SKILLS/SkillSor.cpp')
    paths = source('source/D2Common/src/Path/Path.cpp')
    # Hash reviewed dependencies too; they are not compiled by this bounded probe.
    for path in ['UNIT/SUnitDmg.cpp', 'MISSILES/Missiles.cpp', 'MISSILES/MissMode.cpp']:
        source('source/D2Game/src/' + path)
    source('source/D2Common/src/Units/Missile.cpp')
    cpp = r'''#include <algorithm>
#include <cstdint>
#include <cstdio>
#include <cstdlib>
#include <initializer_list>
#include <vector>
#define __fastcall
#define __stdcall
constexpr int UNITFLAG_SKSRVDOFUNC=64,DAMAGERESULTFLAG_SUCCESSFULHIT=1,PATHTYPE_CHARGEDBOLT=7;
struct Seed{uint32_t low=0;};
struct Path{struct {int X=0,Y=0;}tTargetCoord;int type=0,distance=0,recomputed=0;};using D2DynamicPathStrc=Path;
struct D2UnitStrc{int dwFlags=0,x=0,y=0,total=0,current=0,life=100;Seed pSeed;Path path;Path* pDynamicPath=&path;} owner,target,bolt;
struct D2GameStrc{} game;
struct D2SkillsTxt{int dwCalc[4]={0,1,2,3},nEType=3,nSrcDam=0;} row;
struct D2DamageStrc{int wResultFlags=0,dwEnDmgPct=0,dwConvPct=0,nConvType=0,lightning=0;} saved;
struct Tables{int nMissilesTxtRecordCount=1000;} tables;auto* sgptDataTables=&tables;
struct D2MissileStrc{int dwFlags=0;D2UnitStrc* pOwner=nullptr;int nMissile=0,nX=0,nY=0,nTargetX=0,nTargetY=0,nSkill=0,nSkillLevel=0;void(*pInitFunc)(D2UnitStrc*,int32_t)=nullptr;int pInitArgs=0;};
bool present=true,selected=true,hit=true,moveOnDrain=false,killOnDrain=false;
int missileId=215,allocs=0,elements=0,drains=0,explicitAr=-1,src=-1,count=0,creates=0;
std::vector<D2MissileStrc> packets;
std::vector<int> order;
D2UnitStrc* SUNIT_GetTargetUnit(D2GameStrc*,D2UnitStrc*){return selected?&target:nullptr;}
D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int){return present?&row:nullptr;}
int SUNITDMG_GetResultFlags(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,int ar,int){order.push_back(1);explicitAr=ar;return hit?1:0;}
int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int field,int,int rank){order.push_back(field==0?2:3);return field==0?3+rank/5:0;}
void D2GAME_RollElementalDamage_6FD14DD0(D2UnitStrc*,D2DamageStrc* p,int,int){order.push_back(4);elements++;p->lightning=12345;}
void SUNITDMG_AllocCombat(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,D2DamageStrc* p,int source){order.push_back(5);allocs++;saved=*p;src=source;}
void SUNITDMG_DrainItemDurability(D2GameStrc*,D2UnitStrc*,D2UnitStrc* p,int){order.push_back(6);drains++;if(moveOnDrain){p->x+=10;p->y-=6;}if(killOnDrain)p->life=0;}
int CLIENTS_GetUnitX(D2UnitStrc* p){order.push_back(7);return p->x;}
int CLIENTS_GetUnitY(D2UnitStrc* p){order.push_back(8);return p->y;}
int SKILLS_GetProgressiveSkillMissileId(D2UnitStrc*,int){order.push_back(9);return missileId;}
int MISSILE_GetTotalFrames(D2UnitStrc* p){return p->total;}
void MISSILE_SetTotalFrames(D2UnitStrc* p,int n){p->total=n;}
void MISSILE_SetCurrentFrame(D2UnitStrc* p,int n){p->current=n;}
void SEED_InitLowSeed(Seed* p,int n){p->low=uint32_t(n);}
void PATH_SetType(Path* p,int n){p->type=n;}
void PATH_SetNewDistance(Path* p,uint8_t n){p->distance=n;}
void D2Common_10142(Path* p,D2UnitStrc*,int){p->recomputed++;}
D2UnitStrc* MISSILES_CreateMissileFromParams(D2GameStrc*,D2MissileStrc* p){order.push_back(10);packets.push_back(*p);creates++;return nullptr;}
'''
    for text, name in [(paths, 'D2COMMON_10175_PathGetFirstPointX'),
                       (sor, 'SKILLS_MissileInit_ChargedBolt'),
                       (ama, 'SKILLS_SrvSt06_PowerStrike_ChargedStrike'),
                       (ama, 'SKILLS_SrvDo011_ChargedStrike')]:
        cpp += routine(text, name)
    cpp += r'''int main(){bool first=true;std::printf("{\"flow\":[");
for(int record:{0,1})for(int chosen:{0,1})for(int success:{0,1})for(int mid:{-1,215,999,1000})for(int rank:{1,4,5,20})for(int move:{0,1})for(int kill:{0,1}){
present=record;selected=chosen;hit=success;missileId=mid;moveOnDrain=move;killOnDrain=kill;
owner.dwFlags=0;owner.x=22;owner.y=38;target.x=34;target.y=46;target.life=100;
allocs=elements=drains=creates=0;explicitAr=src=-1;saved={};packets.clear();order.clear();
int start=SKILLS_SrvSt06_PowerStrike_ChargedStrike(&game,&owner,24,rank);
int result=SKILLS_SrvDo011_ChargedStrike(&game,&owner,24,rank);
if(!first)std::printf(",");first=false;
std::printf("{\"input\":[%d,%d,%d,%d,%d,%d,%d],\"result\":[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d],\"packets\":[",record,chosen,success,mid,rank,move,kill,start,result,allocs,elements,drains,explicitAr,src,saved.dwEnDmgPct,saved.lightning,owner.dwFlags);
for(size_t i=0;i<packets.size();i++){auto& p=packets[i];if(i)std::printf(",");std::printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",p.dwFlags,p.pOwner==&owner,p.nMissile,p.nX,p.nY,p.nTargetX,p.nTargetY,p.nSkill,p.nSkillLevel,p.pInitArgs);if(p.pInitFunc!=SKILLS_MissileInit_ChargedBolt)std::abort();}
std::printf("],\"order\":[");for(size_t i=0;i<order.size();i++){if(i)std::printf(",");std::printf("%d",order[i]);}std::printf("]}");
}
std::printf("],\"init\":[");first=true;
for(int frames:{1,76,77,78,98})for(int x:{0,11,34})for(int ordinal:{0,1,6}){
bolt.total=frames;bolt.current=frames;bolt.path={{x,0},0,0,0};bolt.pSeed.low=0;
SKILLS_MissileInit_ChargedBolt(&bolt,ordinal);
if(!first)std::printf(",");first=false;
std::printf("[%d,%d,%d,%d,%d,%u,%d,%d,%d]",frames,x,ordinal,bolt.total,bolt.current,bolt.pSeed.low,bolt.path.type,bolt.path.distance,bolt.path.recomputed);
}SKILLS_MissileInit_ChargedBolt(nullptr,0);std::printf("]}\n");}
'''
    path = STAGE / 'charged-strike-source-probe.cpp'
    path.write_text(cpp)
    binary = STAGE / 'charged-strike-source-probe'
    subprocess.run(['g++', '-std=c++20', '-O2', '-fwrapv', str(path), '-o', str(binary)], check=True)
    data = json.loads(subprocess.check_output([str(binary)]))
    assert len(data['flow']) == 512 and len(data['init']) == 45
    for case in data['flow']:
        record, chosen, hit, mid, rank, move, kill = case['input']
        valid = record and chosen
        start, result, allocs, elements, drains, ar, src, ed, lightning, flags = case['result']
        assert start == bool(valid) and allocs == bool(valid) and drains == bool(valid)
        assert elements == bool(valid and hit) and flags == 64
        assert result == bool(valid and 0 <= mid < 1000)
        assert len(case['packets']) == (3 + rank // 5 if result else 0)
        if valid:
            assert src == 0 and ar == 0
            assert ed == (3 + rank // 5 if hit else 0)
            assert lightning == (12345 if hit else 0)
            assert case['order'].index(5) < case['order'].index(6)
        for i, packet in enumerate(case['packets']):
            x, y = 34 + move * 10, 46 - move * 6
            assert packet == [33, 1, mid, x, y, 2*x-22, 2*y-38, 24, rank, i]
    for frames, x, ordinal, total, current, seed, kind, distance, recomputed in data['init']:
        assert total == current == min(frames, 77)
        assert (seed, kind, distance, recomputed) == (x+ordinal, 7, min(frames, 77), 1)
    fixture = STAGE / 'charged-strike-source-probe.json'
    fixture.write_text(json.dumps(data, separators=(',', ':')) + '\n')
    missiles_path = ROOT / 'tools/reference/d2r-named-equipment/json/missiles.json'
    missiles = json.loads(missiles_path.read_text())
    missile = next(x for x in missiles.values() if x.get('Missile') == 'chargedstrikebolt')
    evidence = dict(referenceVersion='3.3.93847', legacyCommit=tree['sha'], sourceHashes=hashes,
        compiledRoutines=list(blocks), blockHashes={k:hashlib.sha256(v.encode()).hexdigest() for k,v in blocks.items()},
        probe=dict(flowRows=len(data['flow']), initializerRows=len(data['init']), fixture=str(fixture.relative_to(ROOT))),
        modernMissile=missile, modernMissileTableHash=hashlib.sha256(missiles_path.read_bytes()).hexdigest(),
        established=[
            '512 unchanged start6/do11 cases isolate record/target/hit/missile/rank and target movement/death during declared drain. Damage allocation precedes drain; emission reads target coordinates after drain and has no hit/alive guard. Creation failure does not abort remaining ordinal attempts.',
            'Packets carry flags33, target origin,2*target-owner aim, original skill/level and ordinal.45 unchanged initializer cases verify frame77 cap, first-point-X+ordinal seed, charged-bolt path and recalculation. Null initializer input is safe.',
            'Compiled D2COMMON_10175_PathGetFirstPointX actually returns tTargetCoord.X. The seed input is the aim X in source subtiles, not owner/spawn X. The shared native helper must use aim.X*2+ordinal under the current geometry projection.',
            'Shared legacy start6 reads calc1 as physical ED as well as do11 bolt count. At current declared calc1=3+floor(rank/5), successful start emits ED3/3/4/7 at ranks1/4/5/20. Missing SrcDam is passed as0; reviewed FillDamageValues substitutes128. Neither silently dropping physical nor choosing ED0 follows this body.',
            'Reviewed missile damage factory takes Charged Strike skill endpoints with bIncludeMastery1. Missing skill SrcDam remains0 in this distinct missile consumer, so it skips weapon-stat collection; the melee FillDamageValues zero-to128 default must not be applied to bolts. Child rank is supplied at creation while hard synergies/mastery are read there.',
            'Reviewed factory sets activation=range-Activate before calling initializer; chargedstrikebolt current Range98/Activate3 gives95, then initializer reduces total/current to77. Reviewed collision test rejects current>activation, so this composite older path does not imply three delayed frames. This is reviewed dependency reasoning, not a compiled full factory/collision proof.'
        ], limits=[
            'Source preparation is not a native verification, modern executable certificate or class completion. Scalar formula service is declared from current fields; formula interpreter and full combat allocation/RNG are not compiled here.',
            'Declared drain changes coordinates/life only to expose read order; it does not certify modern removal/corpse lifetime. Creation service records attempts and returns null; real missile creation/owner/collision/path/shotgun/damage/persistence remain independent work.',
            'Current shared calc1 ED overlap, direct-hit lightning versus bolts and activation/factory behavior need modern corroboration or explicit acceptance limits. Keep unsupported branches visible.'
        ])
    target = ROOT / 'tools/reference/charged-strike-engine-evidence.json'
    rendered = json.dumps(evidence, indent=2) + '\n'
    if check:
        assert target.read_text() == rendered, 'Charged Strike evidence changed'
    else:
        target.write_text(rendered)
    print(f'Charged Strike: {len(data["flow"])} start/emission and {len(data["init"])} initializer cases compiled; native verification is separate.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
