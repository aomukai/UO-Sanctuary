#!/usr/bin/env python3
"""Execute complete Wake launcher, perpendicular-coordinate accessors and maker.

Allocation, target resolution, collision and registry lookup are declared services.
This establishes producer control flow, not the full missile factory or gameplay.
"""
import argparse
import hashlib
import json
import re
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills'


def build(check=False):
    tree = json.loads((STAGE / 'd2moo-source-tree.json').read_text())
    hashes, blocks = {}, {}

    def routine(relative, name):
        raw = (STAGE / 'd2moo-pinned-source' / relative).read_bytes()
        expected = next(x['sha'] for x in tree['tree'] if x['path'] == relative)
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == expected
        hashes[relative] = hashlib.sha256(raw).hexdigest()
        text = raw.decode()
        match = re.search(r'^.*\b' + name + r'\([^;\n]*\)\s*\n\{', text, re.M)
        assert match, name
        end, depth = text.index('{', match.start()) + 1, 1
        while depth:
            depth += (text[end] == '{') - (text[end] == '}')
            end += 1
        body = text[match.start():end]
        blocks[name] = hashlib.sha256(body.encode()).hexdigest()
        return body + '\n'

    cpp = r'''
#include <cassert>
#include <cstdint>
#include <cstdio>
#include <initializer_list>
#include <vector>
#define __fastcall
#define __stdcall
using BOOL=int;
constexpr int FALSE=0,UNIT_MISSILE=3,UNITFLAG_SKSRVDOFUNC=64,PATH_UNKNOWN_FLAG_0x00008=8;
struct D2GameStrc{};
struct D2DynamicPathStrc{int dwFlags=0;};
struct Coords{int nX=0,nY=0;};
struct MissileData{Coords pTargetCoords;uint16_t nLevel=0;int nSkill=0;};
struct D2UnitStrc{int dwClassId=517,dwUnitType=3,dwFlags=0,x=100,y=102;D2DynamicPathStrc* pDynamicPath=nullptr;MissileData* pMissileData=nullptr;};
struct D2MissilesTxt{int wSubMissile[1]={518};}row;
struct D2SkillsTxt{} skill;
struct Tables{int nMissilesTxtRecordCount=1000;}table;auto* sgptDataTables=&table;
struct D2MissileStrc{int dwFlags=0,nMissile=0,nSkill=0,nSkillLevel=0,nTargetX=0,nTargetY=0;D2UnitStrc* pOwner=nullptr;D2UnitStrc* pOrigin=nullptr;};
D2UnitStrc actor,maker;MissileData md;
bool hasSkill=true,hasRow=true,hasOwner=true;
int missileId=517,aimKind=1,allocation=1,targetCalls=0,phase=0,childFailures=0,hitCalls=0,collisionCalls=0;
std::vector<D2MissileStrc> calls;
D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int id){assert(id==281);return hasSkill?&skill:nullptr;}
D2MissilesTxt* SKILLS_GetMissilesTxtRecord(int id){assert(id==517);return hasRow?&row:nullptr;}
int SKILLS_GetProgressiveSkillMissileId(D2UnitStrc*,int id){assert(id==281);return missileId;}
int D2GAME_GetXAndYFromTargetUnit_6FD14020(D2GameStrc*,D2UnitStrc* u,int* x,int* y){
 targetCalls++;assert(aimKind>=0&&aimKind<6);int points[][2]={{110,112},{110,112},{100,102},{0,112},{110,0},{88,85}};
 *x=points[aimKind][0];*y=points[aimKind][1];return u&&aimKind!=0;
}
int CLIENTS_GetUnitX(D2UnitStrc* u){assert(u);return u->x;}
int CLIENTS_GetUnitY(D2UnitStrc* u){assert(u);return u->y;}
D2UnitStrc* SUNIT_GetOwner(D2GameStrc*,D2UnitStrc* u){assert(u==&maker);return hasOwner?&actor:nullptr;}
int MISSMODE_SrvDmgHitHandler(D2GameStrc*,D2UnitStrc* u,D2UnitStrc* victim,int end){assert(u==&maker&&!victim&&end==0);hitCalls++;return 71;}
int MISSMODE_HandleMissileCollision(D2GameStrc*,D2UnitStrc* u){assert(u==&maker);collisionCalls++;return 42;}
D2UnitStrc* MISSILES_CreateMissileFromParams(D2GameStrc*,D2MissileStrc*);
'''
    common = 'source/D2Common/src/Units/Missile.cpp'
    for name in ['MISSILE_SetTargetX', 'MISSILE_SetTargetY', 'MISSILE_GetTargetX', 'MISSILE_GetTargetY',
                 'MISSILE_SetLevel', 'MISSILE_GetLevel', 'MISSILE_SetSkill', 'MISSILE_GetSkill']:
        cpp += routine(common, name)
    cpp += routine('source/D2Common/src/Path/Step.cpp', 'D2Common_10234')
    cpp += r'''
D2UnitStrc* MISSILES_CreateMissileFromParams(D2GameStrc*,D2MissileStrc* p){
 calls.push_back(*p);
 if(phase){assert(p->dwFlags==2&&p->pOwner==&actor&&p->pOrigin==&maker&&p->nMissile==518);return (childFailures&(1<<(calls.size()-1)))?nullptr:&maker;}
 assert(p->dwFlags==2&&p->pOwner==&actor&&p->pOrigin==&actor&&p->nSkill==281&&p->nMissile==517);
 if(!allocation)return nullptr;
 maker={};md={};maker.dwUnitType=allocation==2?1:3;maker.pMissileData=allocation==3?nullptr:&md;
 MISSILE_SetSkill(&maker,p->nSkill);MISSILE_SetLevel(&maker,p->nSkillLevel);return &maker;
}
'''
    cpp += routine('source/D2Game/src/SKILLS/SkillDruid.cpp', 'SKILLS_SrvDo125_WakeOfDestruction')
    cpp += routine('source/D2Game/src/MISSILES/MissMode.cpp', 'MISSMODE_SrvDo31_WakeOfDestructionMaker_BaalColdMaker')
    cpp += r'''
int main(){D2GameStrc game;bool first=true;printf("{\"launch\":[");
 for(int present:{0,1})for(int skillRow:{0,1})for(int id:{-1,517,1000})for(int aim=0;aim<6;aim++)for(int rank:{1,3,20,255})for(int alloc=0;alloc<4;alloc++){
 actor={};calls.clear();md={};maker={};phase=0;hasSkill=skillRow;missileId=id;aimKind=aim;allocation=alloc;targetCalls=0;
 int result=SKILLS_SrvDo125_WakeOfDestruction(&game,present?&actor:nullptr,281,rank);
 bool reachesTarget=skillRow&&id>=0&&id<1000,creates=reachesTarget&&present&&aim!=0;
 assert(targetCalls==int(reachesTarget)&&calls.size()==size_t(creates));
 assert(result==int(creates&&alloc)&&actor.dwFlags==(creates?64:0));
 int points[][2]={{110,112},{110,112},{100,102},{0,112},{110,0},{88,85}};
 int dx=points[aim][0]-100,dy=points[aim][1]-102;
 if(creates){assert(calls[0].nTargetX==dx&&calls[0].nTargetY==dy&&calls[0].nSkillLevel==rank);}
 bool writable=creates&&alloc==1;
 assert(MISSILE_GetTargetX(&maker)==(writable?-dy:0)&&MISSILE_GetTargetY(&maker)==(writable?dx:0));
 assert(MISSILE_GetSkill(&maker)==(writable?281:0)&&MISSILE_GetLevel(&maker)==unsigned(writable?rank:0));
 printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%zu,%d,%d,%d,%d]",first?"":",",present,skillRow,id,aim,rank,alloc,result,targetCalls,calls.size(),actor.dwFlags,MISSILE_GetTargetX(&maker),MISSILE_GetTargetY(&maker),MISSILE_GetSkill(&maker));first=false;
 }
 printf("],\"maker\":[");first=true;
 for(int present:{0,1})for(int record:{0,1})for(int child:{-1,518})for(int own:{0,1})for(int path:{0,1})for(int flags:{0,8,16,24})for(int aim=1;aim<6;aim++)for(int failures=0;failures<4;failures++){
 actor={};calls.clear();phase=0;hasSkill=true;missileId=517;aimKind=aim;allocation=1;
 assert(SKILLS_SrvDo125_WakeOfDestruction(&game,&actor,281,3)==1);
 int x=MISSILE_GetTargetX(&maker),y=MISSILE_GetTargetY(&maker);
 calls.clear();phase=1;hasRow=record;row.wSubMissile[0]=child;hasOwner=own;childFailures=failures;hitCalls=collisionCalls=0;
 D2DynamicPathStrc p;p.dwFlags=flags;maker.pDynamicPath=path?&p:nullptr;
 int result=MISSMODE_SrvDo31_WakeOfDestructionMaker_BaalColdMaker(&game,present?&maker:nullptr);
 bool valid=present&&record&&child>=0,emit=valid&&own&&path&&(flags&8);
 assert(result==(valid&&own?42:2)&&hitCalls==int(valid&&!own)&&collisionCalls==int(valid&&own)&&calls.size()==size_t(emit?2:0));
 if(emit){assert(calls[0].nTargetX==x&&calls[0].nTargetY==y&&calls[1].nTargetX==-x&&calls[1].nTargetY==-y);for(auto c:calls)assert(c.nSkill==281&&c.nSkillLevel==3);}
 printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%zu,%d,%d]",first?"":",",present,record,child,own,path,flags,aim,failures,result,hitCalls,collisionCalls,calls.size(),x,y);first=false;
 }
 printf("]}\n");}
'''
    source = STAGE / 'wake-of-fire-producer-source-probe.cpp'
    source.write_text(cpp)
    binary = source.with_suffix('')
    subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined',
                    '-fno-sanitize-recover=all', str(source), '-o', str(binary)], check=True)
    result = subprocess.run([str(binary)], capture_output=True)
    (STAGE / 'wake-of-fire-producer-sanitizer.log').write_bytes(result.stderr)
    assert result.returncode == 0 and not result.stderr, result.stderr.decode()[-4000:]
    data = json.loads(result.stdout)
    assert len(data['launch']) == 1152 and len(data['maker']) == 2560
    fixture = STAGE / 'wake-of-fire-producer-source-probe.json'
    fixture.write_text(json.dumps(data, separators=(',', ':')) + '\n')
    evidence = dict(
        referenceVersion='3.3.93847', talentId='assassin.wakeoffire', handlerEnabled=False,
        legacyCommit=tree['sha'], sourceHashes=hashes, routineHashes=blocks,
        fixture=dict(path=str(fixture.relative_to(ROOT)), sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(),
                     launchCases=len(data['launch']), composedMakerCases=len(data['maker'])),
        established=[
            'Complete do125 checks skill, missile bounds and resolved aim before setting flag64 and calling the factory once with flags2, actor owner/origin, skill281 and effective rank. Factory failure returns0 after flag64 is already set.',
            'Successful creation stores the perpendicular (-dy,dx) through complete guarded target setters. Wrong unit type or missing missile data makes those setters no-ops; complete getters return0. The launcher still returns1 for those declared malformed factory controls.',
            'Complete do31 rejects missing maker/record/child before owner lookup. Missing owner calls the hit handler with null target and end0, discards its return, and returns2 without generic collision.',
            'A present owner reaches generic collision exactly once. A dynamic path with flag8 first attempts both opposite wave children, retaining the actor owner, maker origin and stored skill/rank. Either or both factory failures do not suppress the other attempt or final collision. Null paths and other flags emit neither child.',
            'The composed source services include negative, axis-zero and zero relative aim vectors. Producer code does not itself reject a zero vector; this is not proof that the actual factory accepts that vector.'
        ],
        services=[
            'Target resolution, progressive missile lookup, table access, owner registry, missile allocation and collision/hit callbacks are explicit services. Shared complete source aim/path and factory evidence must be composed separately.',
            'The factory service captures exact arguments and initializes skill/rank using complete setters. It does not run real missile creation, damage/stat inheritance, range, velocity, path construction or cleanup.',
            'No current executable equivalence, live actor shot timing, wave footprint, NextHit/LastCollide/DamageRate behavior, post-retirement damage or natural-input acceptance is claimed.'
        ], sanitizers='ASan/UBSan: exit0 and empty runtime stderr required')
    out = ROOT / 'tools/reference/wake-of-fire-producer-source-evidence.json'
    encoded = json.dumps(evidence, indent=2) + '\n'
    if check:
        assert out.read_text() == encoded, 'Wake producer evidence stale'
    else:
        out.write_text(encoded)
    print('Wake producer: 1,152 launcher and 2,560 composed maker cases; complete accessors/path gate; sanitizers clean.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
