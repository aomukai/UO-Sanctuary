#!/usr/bin/env python3
"""Execute complete Vine Attack, head callback and trail hit-router bodies."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / '.local/class-skills'


def build(check=False):
    tree = json.loads((BASE / 'd2moo-source-tree.json').read_text())
    assert tree['sha'] == '0c0bbd1e6bef9181a3da1c046523af622a925416'
    hashes, blocks = {}, {}

    def routine(relative, name):
        raw = (BASE / 'd2moo-pinned-source' / relative).read_bytes()
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(x['sha'] for x in tree['tree'] if x['path'] == relative)
        hashes[relative] = hashlib.sha256(raw).hexdigest()
        source = raw.decode()
        match = re.search(r'^.*\b' + name + r'\([^;\n]*\)\s*\n\{', source, re.M)
        assert match, name
        end, depth = source.index('{', match.start()) + 1, 1
        while depth:
            depth += (source[end] == '{') - (source[end] == '}'); end += 1
        body = source[match.start():end]
        blocks[name] = hashlib.sha256(body.encode()).hexdigest()
        return body

    pins = {}
    manifest = json.loads((ROOT / 'tools/reference/d2r-named-equipment/sources.json').read_text())
    for name in ['skills', 'missiles']:
        raw = (ROOT / 'tools/reference/d2r-named-equipment/json' / (name + '.json')).read_bytes()
        pins[name] = hashlib.sha256(raw).hexdigest()
        assert pins[name] == manifest['files']['json/' + name + '.json']['sha256']
        if name == 'skills':
            skill = json.loads(raw)['294']
            assert skill['srvdofunc'] == 130 and skill['srvmissilea'] == 'plague vines' and skill['calc1'] == '"min(ln12,24)"' and skill['Param1'] == 3 and skill['Param2'] == 1
        else:
            rows = json.loads(raw); head = rows['plague vines']; trail = rows['plague vines trail']
            assert head['pSrvDoFunc'] == 26 and head['Param1'] == 9 and head['SubMissile1'] == 'plague vines trail'
            assert trail['pSrvHitFunc'] == 50 and trail['sHitPar1'] == 15 and trail['AlwaysExplode'] == trail['LastCollide'] == 1
            assert not any(trail.get(k) for k in ['ToHit', 'CollideKill', 'NextHit', 'Pierce', 'Explosion', 'CollideFriend', 'pSrvDmgFunc'])
    mode = 'source/D2Game/src/MISSILES/MissMode.cpp'
    common = 'source/D2Common/src/Units/Missile.cpp'
    router = routine(mode, 'MISSMODE_SrvDmgHitHandler')
    cpp = r'''#include <algorithm>
#include <cstdint>
#include <cstdio>
#include <iterator>
#include <map>
#include <vector>
#define __fastcall
#define __stdcall
using BOOL=int;
constexpr int TRUE=1,FALSE=0,UNIT_PLAYER=0,UNIT_MONSTER=1,UNIT_MISSILE=3,D2UnitInvalidGUID=-1;
constexpr int UNITFLAG_SKSRVDOFUNC=64,MISSILESFLAGINDEX_EXPLOSION=0,MISSILESFLAGINDEX_PIERCE=1,MISSILESFLAGINDEX_LASTCOLLIDE=2;
constexpr int MISSMODE_NOCOLLIDE=0,MISSMODE_PLAYERKILL=1,MISSMODE_MONSTERKILL=2,STATE_JUSTHIT=1,EVENTTYPE_REMOVESTATE=1,UNITEVENT_HITBYMISSILE=1;
constexpr int STAT_SKILL_PIERCE=3,STAT_ITEM_PIERCE=4,STAT_PIERCE_IDX=5,STAT_TOHIT=6,STAT_ITEM_DAMAGETARGETAC=7,STAT_ARMORCLASS=8;
struct D2GameStrc{int dwGameFrame=1000;void* pMemoryPool=nullptr;}game;
struct D2ActiveRoomStrc{}room;struct D2StatListStrc{};
struct D2MissileDataStrc{int nOwnerType=0,dwOwnerGUID=-1;};
struct D2UnitStrc{int dwClassId=475,dwUnitType=3,dwUnitId=10,dwMissileMode=3,dwFlags=0,remaining=125,total=125,rank=1,skill=294;
 D2GameStrc* pGame=&game;D2MissileDataStrc md;D2MissileDataStrc* pMissileData=nullptr;std::map<int,int>stats;}parent,owner,target;
struct D2MissilesTxt{unsigned dwMissileFlags=4;int nCollideKill=0,nToHit=0,nNextHit=0,nNextDelay=0,nAlwaysExplode=1,wSrvHitFunc=50,wSrvDmgFunc=0,nCollideFriend=0,dwHitPar[1]={15},wSubMissile[1]={475},dwParam[1]={9};}row;
struct D2SkillsTxt{int wSrvMissileA=474,dwCalc[1]={1};}skill;
struct Tables{int nMissilesTxtRecordCount=700;}tables;Tables* sgptDataTables=&tables;
struct D2DamageStrc{};
using InitFunc=void(*)(D2UnitStrc*,int);void SKILLS_MissileInit_ChargedBolt(D2UnitStrc*,int){}
struct D2MissileStrc{int nSkillLevel=0,nMissile=0,dwFlags=0,nSkill=0,nX=0,nY=0,nTargetX=0,nTargetY=0,pInitArgs=0;D2UnitStrc*pOwner=nullptr;InitFunc pInitFunc=nullptr;};
unsigned gdwBitMasks[32];bool hasRecord=true,hasOwner=true,hasSkill=true,hasTarget=true,hostile=true,allocation=true;int formula=3,created=0,collisions=0,fillCalls=0,flagCalls=0,arCalls=0,resets=0,hitEvents=0,order=0,createOrder=0,collisionOrder=0;
std::vector<D2MissileStrc> requests;
D2MissilesTxt* SKILLS_GetMissilesTxtRecord(int){return hasRecord?&row:nullptr;}
D2MissilesTxt* DATATBLS_GetMissilesTxtRecord(int){return &row;}
D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int){return hasSkill?&skill:nullptr;}
int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int,int,int){return formula;}
int D2GAME_GetXAndYFromTargetUnit_6FD14020(D2GameStrc*,D2UnitStrc*,int*x,int*y){*x=40;*y=70;return hasTarget;}
D2UnitStrc* SUNIT_GetOwner(D2GameStrc*,D2UnitStrc*){return hasOwner?&owner:nullptr;}
int MISSILE_GetRemainingFrames(D2UnitStrc*p){return p->remaining;}int MISSILE_GetTotalFrames(D2UnitStrc*p){return p->total;}
int MISSILE_GetSkill(D2UnitStrc*p){return p->skill;}int MISSILE_GetLevel(D2UnitStrc*p){return p->rank;}
int CLIENTS_GetUnitX(D2UnitStrc*){return 40;}int CLIENTS_GetUnitY(D2UnitStrc*){return 70;}
D2UnitStrc* MISSILES_CreateMissileFromParams(D2GameStrc*,D2MissileStrc*p){created++;requests.push_back(*p);return allocation?&parent:nullptr;}
D2UnitStrc* D2GAME_CreateMissile_6FD115E0(D2GameStrc*,D2UnitStrc*p,int sk,int rank,int id,int x,int y){created++;createOrder=++order;D2MissileStrc a;a.pOwner=p;a.nSkill=sk;a.nSkillLevel=rank;a.nMissile=id;a.nX=x;a.nY=y;requests.push_back(a);return allocation?&parent:nullptr;}
int MISSMODE_HandleMissileCollision(D2GameStrc*,D2UnitStrc*){collisions++;collisionOrder=++order;return 42;}
int STATLIST_UnitGetStatValue(D2UnitStrc*p,int s,int){return p->stats[s];}int STATLIST_UnitGetItemStatOrSkillStatValue(D2UnitStrc*p,int s,int){return p->stats[s];}
void STATLIST_SetUnitStat(D2UnitStrc*p,int s,int v,int){p->stats[s]=v;}
bool STATES_CheckState(D2UnitStrc*,int){return false;}bool sub_6FCBD900(D2GameStrc*,D2UnitStrc*,D2UnitStrc*){return hostile;}
bool SUNITDMG_IsHitSuccessful(D2UnitStrc*,D2UnitStrc*,int,int){arCalls++;return true;}
D2StatListStrc* STATLIST_AllocStatList(void*,unsigned,int,int,int){return nullptr;}
void EVENT_SetEvent(D2GameStrc*,D2UnitStrc*,int,int,int,int){}void STATLIST_SetState(D2StatListStrc*,int){}
void MISSMODE_ToggleStateOff(D2UnitStrc*,int,D2StatListStrc*){}
void STATLIST_SetStatRemoveCallback(D2StatListStrc*,void(*)(D2UnitStrc*,int,D2StatListStrc*)){}
void D2COMMON_10475_PostStatToStatList(D2UnitStrc*,D2StatListStrc*,int){}void STATES_ToggleState(D2UnitStrc*,int,int){}
void SUNITEVENT_Trigger(D2GameStrc*,int,D2UnitStrc*,D2UnitStrc*,void*){hitEvents++;}
void MISSMODE_FillDamageParams(D2UnitStrc*,D2UnitStrc*,D2DamageStrc*){fillCalls++;}
void MISSMODE_SetDamageFlags(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,D2DamageStrc*){flagCalls++;}
int MONSTERS_GetHirelingTypeId(D2UnitStrc*){return 0;}
int UNITS_GetUnitSizeX(D2UnitStrc*){return 3;}D2ActiveRoomStrc* UNITS_GetRoom(D2UnitStrc*){return &room;}
void COLLISION_ResetMaskWithSize(D2ActiveRoomStrc*,int,int,int,unsigned){resets++;}
'''
    cpp += routine(common, 'MISSILE_SetOwner') + '\n' + routine(common, 'MISSILE_CheckUnitIfOwner') + '\n'
    cpp += routine(mode, 'MISSMODE_SrvHit50_PlagueVinesTrail') + '\n'
    for name in sorted(set(re.findall(r'\bMISSMODE_Srv(?:Hit|Dmg)\w+', router)) - {'MISSMODE_SrvDmgHitHandler', 'MISSMODE_SrvHit50_PlagueVinesTrail'}):
        kind = 'void' if 'SrvDmg' in name else 'int32_t'
        params = 'D2GameStrc*,D2UnitStrc*,D2UnitStrc*' + (',D2DamageStrc*' if kind == 'void' else '')
        cpp += f'{kind} {name}({params}){{' + ('' if kind == 'void' else 'return 3;') + '}\n'
    cpp += router + '\n'
    cpp += routine(mode, 'MISSMODE_SrvDo26_Vines_PlagueVines') + '\n'
    cpp += routine('source/D2Game/src/SKILLS/SkillDruid.cpp', 'SKILLS_SrvDo130_VineAttack') + r'''
void reset(){parent={};owner={};target={};parent.pMissileData=&parent.md;target.dwUnitType=UNIT_MONSTER;row={};skill={};hasRecord=hasOwner=hasSkill=hasTarget=hostile=allocation=true;created=collisions=fillCalls=flagCalls=arCalls=resets=hitEvents=order=createOrder=collisionOrder=0;requests.clear();}
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;bool first=true;printf("{\"launch\":[");
for(int rank=1;rank<=255;rank++)for(int failure=0;failure<7;failure++){
 reset();hasSkill=failure!=1;skill.wSrvMissileA=failure==2?0:failure==3?700:474;formula=failure==4?0:std::min(rank+2,24);hasTarget=failure!=5;allocation=failure!=6;
 int result=SKILLS_SrvDo130_VineAttack(&game,&owner,294,rank);
 printf("%s[%d,%d,%d,%d,[",first?"":",",rank,failure,result,owner.dwFlags);first=false;bool firstRequest=true;
 for(auto&a:requests){printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",firstRequest?"":",",a.nX,a.nY,a.nTargetX,a.nTargetY,a.nMissile,a.nSkill,a.nSkillLevel,a.dwFlags,a.pInitArgs,a.pOwner==&owner,a.pInitFunc==SKILLS_MissileInit_ChargedBolt);firstRequest=false;}printf("]]");
}
printf("],\"head\":[");first=true;
for(int frame=0;frame<=24;frame++)for(int parameter:{-1,0,9})for(int failure=0;failure<6;failure++){
 reset();parent.remaining=frame;parent.rank=7;parent.dwClassId=474;row.dwParam[0]=parameter;row.wSubMissile[0]=failure==2?0:475;hasRecord=failure!=1;hasOwner=failure!=3;allocation=failure!=4;
 int result=MISSMODE_SrvDo26_Vines_PlagueVines(&game,failure==0?nullptr:&parent);
 printf("%s[%d,%d,%d,%d,%d,%d,%d,%d",first?"":",",frame,parameter,failure,result,created,collisions,createOrder,collisionOrder);first=false;
 if(requests.empty())printf(",[]]");else{auto&a=requests[0];printf(",[%d,%d,%d,%d,%d,%d]]",a.pOwner==&owner,a.nSkill,a.nSkillLevel,a.nMissile,a.nX,a.nY);}
}
printf("],\"trail\":[");first=true;
for(int total:{125,600,6475})for(int age:{0,1,14,15,16,17,124,125,6475})for(int victim:{0,1})for(int ending:{0,1})for(int repeat:{0,1})for(int own:{0,1})for(int enemy:{0,1}){
 reset();parent.total=total;parent.remaining=total-age;hasOwner=own;hostile=enemy;
 if(repeat){parent.md.nOwnerType=UNIT_MONSTER;parent.md.dwOwnerGUID=10;}
 int result=MISSMODE_SrvDmgHitHandler(&game,&parent,victim?&target:nullptr,ending);
 printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",total,age,victim,ending,repeat,own,enemy,result,fillCalls,flagCalls,parent.md.dwOwnerGUID,hitEvents,resets);first=false;
}
printf("],\"sequence\":[");first=true;
reset();for(int age:{0,15,16,17,18,19}){
 parent.remaining=parent.total-age;target.dwUnitId=age==17?20:10;int before=fillCalls;int result=MISSMODE_SrvDmgHitHandler(&game,&parent,&target,0);
 printf("%s[%d,%d,%d,%d,%d]",first?"":",",age,target.dwUnitId,result,fillCalls-before,parent.md.dwOwnerGUID);first=false;
}printf("]}\n");}
'''
    path = BASE / 'creeper-vines-source-probe.cpp'; path.write_text(cpp)
    binary = path.with_suffix('')
    subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined', '-fno-sanitize-recover=all', str(path), '-o', str(binary)], check=True)
    result = subprocess.run([str(binary)], capture_output=True)
    (BASE / 'creeper-vines-sanitizer.log').write_bytes(result.stderr)
    assert result.returncode == 0 and not result.stderr, result.stderr.decode()[-4000:]
    data = json.loads(result.stdout)
    assert [len(data[k]) for k in ['launch', 'head', 'trail', 'sequence']] == [1785, 450, 864, 6]
    offsets = [(0, 1), (0, -1), (1, 0), (-1, 0)]
    for rank, failure, returned, flags, requests in data['launch']:
        assert returned == int(failure in [0, 5, 6]) and flags == (0 if failure in [1, 2, 3] else 64)
        count = min(rank + 2, 24) if failure in [0, 6] else 0
        assert len(requests) == count
        for i, request in enumerate(requests):
            assert request == [40, 70, *offsets[i % 4], 474, 294, rank, 3, i, 1, 1]
    for frame, parameter, failure, returned, created, collisions, made, collided, requests in data['head']:
        valid = failure in [3, 4, 5]
        create = bool(valid and frame % max(parameter, 1) == 0)
        assert [returned, created, collisions, made, collided] == [42 if valid else 2, int(create), int(valid), int(create), (2 if create else 1) if valid else 0]
        assert requests == ([int(failure != 3), 294, 7, 475, 40, 70] if create else [])
    for total, age, victim, ending, repeat, own, enemy, returned, fill, flags, last, events, resets in data['trail']:
        rejected = bool(victim and (repeat or own and not enemy))
        active = bool(victim and not rejected and age > 15)
        assert fill == flags == int(active)
        assert last == (10 if victim or repeat else -1)
        assert events == int(not rejected) and returned == (2 if ending and not rejected else 1)
        assert resets == int(bool(ending and not rejected))
    assert data['sequence'] == [[0, 10, 1, 0, 10], [15, 10, 1, 0, 10], [16, 10, 1, 0, 10], [17, 20, 1, 1, 20], [18, 10, 1, 1, 10], [19, 10, 1, 0, 10]]
    fixture = BASE / 'creeper-vines-source-probe.json'
    fixture.write_text(json.dumps(data, separators=(',', ':')) + '\n')
    evidence = dict(referenceVersion='3.3.93847', legacyCommit=tree['sha'], handlerEnabled=False, installed=False,
                    sourceHashes=hashes, pinnedTableHashes=pins, unchangedRoutineHashes=blocks, generatedSourceSha256=hashlib.sha256(path.read_bytes()).hexdigest(),
                    fixture=dict(path=str(fixture.relative_to(ROOT)), sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(),
                                 launch=1785, head=450, trail=864, sequence=6), sanitizers='ASan/UBSan exit0 with empty runtime stderr.',
                    established=[
                        'Complete do130 uses target coordinates and cyclic cardinal offsets, flags3, stored skill294/rank, charged initializer and index initarg for min(rank+2,24) heads. Missing target returns1 with no requests; failed allocations do not stop the loop or change return1. Invalid skill/missile/formula controls retain distinct owner-flag boundaries.',
                        'Complete head do26 requests a fresh trail before generic collision when remainingFrames modulo max(Param1,1) equals0. Missing owner still makes a coordinate-factory request with null owner; allocation acceptance is separate. Current Param1=9 is not a poison damage interval.',
                        'Complete hit50 plus full hit router suppress damage through age15inclusive and permit damage after that threshold. The surviving trail has no AR or CollideKill. Null-unit terminal dispatch causes removal; a supplied-unit ordinary contact remains alive. Total-frame values are supplied boundary controls, not proof current Range formula creates those lifetimes.',
                        'Full LastCollide records a victim before the hit50 age gate and before hostility refusal. A same-most-recent victim is then ignored even after age16; alternating recipients replace that record. The sequential control captures early contact0, repeated15/16, different victim17, original18 and repeated19. Do not replace this with a permanent per-mat set or an invented periodic damage lease.'
                    ], limits=[
                        'Legacy bodies plus current table values are not proof the modern executable retains this age-gate/LastCollide combination. Full fill/flags/damage, world finder/collision, hostility, events, allocation, target/formula and charged initializer are bounded services here.',
                        'Head movement, factory Range expression evaluation, current poison owner/Rabies/mastery/pierce transport and capture time, actor state/AI/targetability, actual poison application, saved world and learned casting remain separate. No Creeper handler is enabled.'
                    ])
    out = ROOT / 'tools/reference/creeper-vines-source-evidence.json'; encoded = json.dumps(evidence, indent=2) + '\n'
    if check:
        assert out.read_text() == encoded
    else:
        out.write_text(encoded)
    print('Creeper vines:1785 launch,450 head,864 full hit-router and6 sequential LastCollide cases pass; current engine/factory/actor remain separate.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__); parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
