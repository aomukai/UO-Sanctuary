#!/usr/bin/env python3
"""Compile complete legacy common-pet and six-mode movement policies."""
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
    relative = 'source/D2Game/src/AI/AiThink.cpp'
    raw = (BASE / 'd2moo-pinned-source' / relative).read_bytes()
    assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(x['sha'] for x in tree['tree'] if x['path'] == relative)
    source = raw.decode()

    def routine(name):
        match = re.search(r'^int32_t __fastcall ' + name + r'\([^\n]+\)\s*\n\{', source, re.M)
        assert match, name
        end, depth = source.index('{', match.start()) + 1, 1
        while depth:
            depth += (source[end] == '{') - (source[end] == '}'); end += 1
        return source[match.start():end]

    bodies = {name: routine(name) for name in ['D2GAME_AI_PetMove_6FCE2BA0', 'sub_6FCE34E0']}
    cpp = r'''#include <algorithm>
#include <cstdint>
#include <cstdio>
#include <cstdlib>
#include <initializer_list>
#include <vector>
#define __fastcall
constexpr int UNIT_MONSTER=1,PLRMODE_WALK=2,EVENTTYPE_AITHINK=1,MONSTER_NECROSKELETON=0,UNITFLAGEX_TELEPORTED=1;
struct D2AiParamStrc{}parameter;
struct D2MonsterDataStrc{D2AiParamStrc*pAiParam=&parameter;}monster;
struct D2DynamicPathStrc{int fx=210,fy=230,tx=210,ty=230;}ownerPath;
struct D2UnitStrc{int dwUnitType=1,dwAnimMode=1,x=100,y=100,dwFlagEx=0;D2MonsterDataStrc*pMonsterData=&monster;D2DynamicPathStrc*pDynamicPath=&ownerPath;}unit,owner;
struct D2GameStrc{}game;struct D2AiTickParamStrc{}ai;struct D2ActiveRoomStrc{}room;
struct D2PlayerDataStrc{int unk0xA0=0,unk0xA8[42]={};}player;
struct D2PetMoveStrc{D2UnitStrc*pUnit=nullptr,*pTarget=nullptr;int nCounter=0;};
struct Request{int kind,a=0,b=0,c=0,d=0;};std::vector<Request>actions;
int distanceValue=0,petCount=0,direction=0,roomPattern=0,successAt=0,attempts=0,crowd=0,placement=7,percent=99;
uint32_t randomValue=0;
void log(int kind,int a=0,int b=0,int c=0,int d=0){actions.push_back({kind,a,b,c,d});}
int act(int kind,int a=0,int b=0,int c=0,int d=0){log(kind,a,b,c,d);if(kind==12)return crowd;if(kind==9)return (placement&4)!=0;if(kind==1||kind==2||kind==3||kind==4||kind==5||kind==13||kind==14){attempts++;return successAt>0&&attempts==successAt;}return 0;}
D2PlayerDataStrc* UNITS_GetPlayerData(D2UnitStrc*){return &player;}
int D2Common_10224(D2DynamicPathStrc*p){return p->tx;}int D2Common_10225(D2DynamicPathStrc*p){return p->ty;}
int D2COMMON_10175_PathGetFirstPointX(D2DynamicPathStrc*p){return p->fx;}int D2COMMON_10176_PathGetFirstPointY(D2DynamicPathStrc*p){return p->fy;}
int CLIENTS_GetUnitX(D2UnitStrc*p){return p->x;}int CLIENTS_GetUnitY(D2UnitStrc*p){return p->y;}
D2ActiveRoomStrc* UNITS_GetRoom(D2UnitStrc*){return &room;}
int UNITS_GetDirectionToCoords(D2UnitStrc*,int,int){return direction;}int D2Common_11053(int d){return d;}
void D2Common_11055(int d,int*x,int*y){int offsets[8][2]={{0,1},{-1,1},{-1,0},{-1,-1},{0,-1},{1,-1},{1,0},{1,1}};*x=offsets[d][0];*y=offsets[d][1];log(17,d,*x,*y);}
int D2Common_10095(D2ActiveRoomStrc*,int x,int y){int value=roomPattern==0?1:roomPattern==1?(y>=220?1:2):(x==200&&y==220?1:0);log(15,x,y,value);return value;}
int AIUTIL_GetDistanceToCoordinates_FullUnitSize(D2UnitStrc*,D2UnitStrc*){return distanceValue;}
int AIUTIL_GetDistanceToCoordinates(D2UnitStrc*,int x,int y){int value=std::abs(x-unit.x)+std::abs(y-unit.y);log(16,x,y,value);return value;}
void AITACTICS_SetVelocity(D2UnitStrc*,int first,int velocity,int cap){act(0,first,velocity,cap);}
int AITACTICS_RunToTargetCoordinatesDeleteAiEvent(D2GameStrc*,D2UnitStrc*,int x,int y){return act(2,x,y);}
int AITACTICS_WalkToTargetCoordinatesDeleteAiEvent(D2GameStrc*,D2UnitStrc*,int x,int y){return act(1,x,y);}
int AITACTICS_WalkToTargetCoordinates(D2GameStrc*,D2UnitStrc*,int x,int y){return act(3,x,y);}
int AITACTICS_RunToTargetCoordinates(D2GameStrc*,D2UnitStrc*,int x,int y){return act(4,x,y);}
int AITACTICS_WanderToTarget(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,int value){return act(5,value);}
void D2GAME_EVENTS_Delete_6FC34840(D2GameStrc*,D2UnitStrc*,int,int){act(6);}
uint32_t ITEMS_RollRandomNumber(D2UnitStrc*){log(18,(int64_t)randomValue>>16,randomValue&65535);return randomValue;}
// Source takes &unit.pSeed; retain that call shape independently of the unit.
uint32_t ITEMS_RollRandomNumber(uint32_t*){log(18,randomValue>>16,randomValue&65535);return randomValue;}
int AI_RollPercentage(D2UnitStrc*){log(19,percent);return percent;}
void AITACTICS_IdleInNeutralMode(D2GameStrc*,D2UnitStrc*,int value){act(7,value);}
void D2GAME_MONSTERMODE_SetVelocityParams_6FC62FC0(D2AiParamStrc*p,int first,int velocity,int cap){act(8,first,velocity,cap,p!=nullptr);}
int D2Common_10096(D2ActiveRoomStrc*,int,int){return 0;}
int sub_6FC66260(D2GameStrc*,D2ActiveRoomStrc*,int,int,int*x,int*y,int){log(20);if(!(placement&1))return 0;*x=501;*y=502;return 1;}
D2ActiveRoomStrc* D2GAME_GetRoom_6FC52070(D2ActiveRoomStrc*,int x,int y){log(21,x,y);return placement&2?&room:nullptr;}
int sub_6FCBDFE0(D2GameStrc*,D2UnitStrc*,D2ActiveRoomStrc*,int x,int y,int,int){return act(9,x,y);}
void UNITROOM_RefreshUnit(D2UnitStrc*){act(10);}
void sub_6FCE34B0(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,void*){}
void PLAYERPETS_IteratePets(D2GameStrc*,D2UnitStrc*,void(*)(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,void*),D2PetMoveStrc*p){p->nCounter=act(12);}
int D2GAME_AICORE_Escape_6FCD0560(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,int leash,int flag){return act(13,leash,flag);}
int D2GAME_AICORE_WalkToOwner_6FCD0B60(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,int leash){return act(14,leash);}
int PLAYERPETS_GetTotalPetCount(D2UnitStrc*){return petCount;}
bool DUNGEON_IsRoomInTown(D2ActiveRoomStrc*){extern bool town;return town;}
bool town=false;
'''
    # Marking the source unit's flag is an assignment rather than a service.
    # Observe it in the final result; no invented marker callback is injected.
    cpp = cpp.replace('D2DynamicPathStrc*pDynamicPath=&ownerPath;', 'D2DynamicPathStrc*pDynamicPath=&ownerPath;uint32_t pSeed=0;')
    cpp += '\n'.join(bodies.values()) + r'''
bool first=true;int total=0;
void emit(int family,int mode,int run,int speed,int leash,int ownerMode,int distance,int pets,int path,int dir,int cursor,int trail,int recent,int rooms,int success,uint32_t random,int percentage,int neighbors,int places,int hasAi,int hasTarget,int inTown,int combat,int flag){
owner.x=200;owner.y=220;unit.x=100;unit.y=100;owner.dwAnimMode=ownerMode;unit.dwFlagEx=0;unit.pMonsterData=hasAi?&monster:nullptr;
ownerPath.fx=210+(path&1?1:0);ownerPath.fy=230+(path&2?1:0);ownerPath.tx=210;ownerPath.ty=230;
distanceValue=distance;petCount=pets;direction=dir;player={};player.unk0xA0=cursor;
for(int i=0;i<20;i++){player.unk0xA8[2*i]=trail==0?0:trail==2&&i%2==0?0:100+2*i;player.unk0xA8[2*i+1]=trail==0?0:trail==2&&i%2!=0?0:100-i;}
player.unk0xA8[40]=100+recent;player.unk0xA8[41]=100;roomPattern=rooms;successAt=success;randomValue=random;percent=percentage;crowd=neighbors;placement=places;town=inTown;actions.clear();attempts=0;
int result=family==0?D2GAME_AI_PetMove_6FCE2BA0(&game,&owner,&unit,mode,run,speed,leash):sub_6FCE34E0(&game,&unit,hasTarget?&owner:nullptr,&owner,combat,&ai,flag,leash);
printf("%s[[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%u,%d,%d,%d,%d,%d,%d,%d,%d],%d,%d,%d,[",first?"":",",family,mode,run,speed,leash,ownerMode,distance,pets,path,dir,cursor,trail,recent,rooms,success,random,percentage,neighbors,places,hasAi,hasTarget,inTown,combat,flag,result,attempts,unit.dwFlagEx);first=false;bool firstAction=true;
for(auto&a:actions){printf("%s[%d,%d,%d,%d,%d]",firstAction?"":",",a.kind,a.a,a.b,a.c,a.d);firstAction=false;}printf("]]");total++;
}
int main(){printf("[");
for(int dir=0;dir<8;dir++)for(int run:{0,1})for(int rooms:{0,1,2})for(int success:{0,1,2,7,14,15,16,17,18})emit(0,0,run,37,6,1,20,0,0,dir,0,0,28,rooms,success,0,99,0,7,1,0,0,0,0);
for(int ownerMode:{1,2,6})for(int run:{0,1})for(int speed:{0,25})for(int cursor:{0,1,19})for(int trail:{0,1,2})for(int success:{0,1,2,3,4,5,20,41,42})for(uint32_t random:{0u,39u,40u,4294967295u})for(int hasAi:{0,1})emit(0,1,run,speed,6,ownerMode,27,0,0,0,cursor,trail,28,0,success,random,99,0,7,hasAi,0,0,0,0);
for(int percentage:{0,9,10,99})for(uint32_t random:{0u,1u,2u,4294967295u})for(int success:{0,1,2})emit(0,2,0,0,6,1,10,0,0,0,0,0,28,0,success,random,percentage,0,7,1,0,0,0,0);
for(int places=0;places<8;places++)emit(0,3,0,0,6,1,81,0,0,0,0,0,28,0,0,0,99,0,places,1,0,0,0,0);
for(int mode:{4,5,6,-1})for(int neighbors:{0,1,3})for(int success:{0,1,2,3})emit(0,mode,0,0,36,1,1,0,0,0,0,0,28,0,success,0,99,neighbors,7,1,0,0,0,0);
for(int distance:{0,1,2,6,7,27,28,29,30,50,51,80,81})for(int ownerMode:{1,2,3,6})for(int pets:{0,40,80})for(int path:{0,1,2,3})for(int recent:{27,28})for(int rooms:{0,1})for(int hasTarget:{0,1})for(int inTown:{0,1})for(int combat:{0,1})for(int flag:{0,1})emit(1,0,0,0,6,ownerMode,distance,pets,path,0,19,1,recent,rooms,1,39,99,1,7,1,hasTarget,inTown,combat,flag);
printf("]\n");}
'''
    path = BASE / 'pet-movement-source-probe.cpp'; path.write_text(cpp)
    binary = path.with_suffix('')
    subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined', '-fno-sanitize-recover=all', str(path), '-o', str(binary)], check=True)
    result = subprocess.run([str(binary)], capture_output=True)
    (BASE / 'pet-movement-sanitizer.log').write_bytes(result.stderr)
    assert result.returncode == 0 and not result.stderr, result.stderr.decode()[-4000:]
    cases = json.loads(result.stdout)
    direct = sum(row[0][0] == 0 for row in cases); common = len(cases) - direct
    assert direct == 8312 and common == 39936, (direct, common)
    fixture = BASE / 'pet-movement-source-probe.json'; fixture.write_text(json.dumps(cases, separators=(',', ':')) + '\n')
    evidence = dict(referenceVersion='3.3.93847', legacyCommit=tree['sha'], sourceHashes={relative: hashlib.sha256(raw).hexdigest()},
        bodyHashes={name: hashlib.sha256(body.encode()).hexdigest() for name, body in bodies.items()},
        generatedSourceSha256=hashlib.sha256(cpp.encode()).hexdigest(),
        fixture=dict(path=str(fixture.relative_to(ROOT)), sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(), direct=direct, common=common),
        sanitizer='ASan/UBSan clean',
        established=[
            'Complete unchanged PetMove modes0..5 and common pet decision body execute together, preserving ordered velocity/path/event/idle/placement requests, return values and teleported flag writes.',
            'Mode0 tries seven opposing owner-route offsets, each eligible-room full and midpoint path, then wander/run-midpoint/walk-midpoint/direct-owner fallbacks. Mode1 scans all20 prior owner history entries in reverse wrapping order, skips zero/near points, retains random40..79speed and one velocity-parameter fallback, then distance/4minimum4wander.',
            'Modes2/4/5 retain10percent wander and idle paths, declared crowd/escape behavior, owner-walk and exact event deletion ordering. Mode3 uses the source skeleton-placement request, room/commit failures and refresh/teleported/idle5only on success.',
            'Common pet policy incorporates half total-pet count plus leash capped36, owner mode/path/room and distances1/30/50/80. The owner first/final coordinate test is literal AND. Existing target outside town suppresses common movement through the80boundary; a nonzero flag suppresses mode2only. Player-data words40/41use a strict28boundary; the separately pinned corrected source names them the owner last-teleport point.'
        ], services=[
            'Owner/player/monster data and dynamic-path fields, initial heading/octant and offset lookup, distance/full-size and room services are declared. Coordinate-distance service uses a supplied Manhattan test metric; it does not certify D2 geometry.',
            'Each tactics/path/escape/owner-walk request has a controlled first-success ordinal. Random raw and percentage values, placement/room/commit outcomes and nearby-pet count are supplied, with exact call ordering recorded. No world mode/path initialization is executed.',
            'Crowd enumeration supplies its output count without executing sub_6FCE34B0. Its old reconstructed prior-target pointer is defective; separately pinned7f6d773uses the current pet instead. Actual callback enumeration still needs its own consumer acceptance. Null monster AI parameter is delivered to a declared setter boundary rather than dereferenced.'
        ], limits=[
            'Legacy source evidence does not establish exact current D2R policy, owner history sampling/ordering, crowd callback interpretation, heading/geometry, RNG stream or NPC mode/path/animation/event execution.',
            'No actual wolf follow, teleport, AI clock or ordinary attack/learned cast is inferred from these bounded services. Native policy comparison and actual actor composition have separate acceptance.'
        ])
    destination = ROOT / 'tools/reference/pet-movement-source-evidence.json'; encoded = json.dumps(evidence, indent=2) + '\n'
    if check:
        assert destination.read_text() == encoded
    else:
        destination.write_text(encoded)
    print(f'Complete pet movement:{direct}direct/{common}common contexts, clean ASan/UBSan; world services explicit.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__); parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
