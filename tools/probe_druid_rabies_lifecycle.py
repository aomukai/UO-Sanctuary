#!/usr/bin/env python3
"""Compose complete Rabies emitter/basic collision clocks and zeroed missile metadata."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
from review_weapon_event_order import function

ROOT=Path(__file__).resolve().parents[1]
BASE=ROOT/'.local/class-skills'


def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()


def build(check=False):
    tree=json.loads((BASE/'d2moo-source-tree.json').read_text());pin=json.loads((ROOT/'tools/reference/druid-rabies-preflight.json').read_text())
    assert tree['sha']==pin['legacyCommit'];routines={};hashes={}
    def routine(relative,name):
        path=BASE/'d2moo-pinned-source'/relative;raw=path.read_bytes()
        assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==relative)
        body=function(raw.decode(),name);hashes[relative]=sha(path);routines[name]=hashlib.sha256(body.encode()).hexdigest();return body+'\n'
    cpp=r'''
#include <algorithm>
#include <cassert>
#include <cstdint>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <vector>
#define __fastcall
#define __stdcall
#define D2_ALLOC_STRC_POOL(pool,type) new type
constexpr int UNIT_MISSILE=3,D2UnitInvalidGUID=-1,MISSMODE_NOCOLLIDE=0,MISSMODE_BARRIERCOLLIDE=4;
struct Coord{int nX=0,nY=0;};struct Seed{};
struct D2MissileDataStrc{int dwOwnerGUID=0,nCurrentFrame=0,nTotalFrames=0,nActivateFrame=0;Coord pTargetCoords;};
struct D2UnitStrc{int dwUnitType=3,dwClassId=515,dwMissileMode=3,skill=238,rank=1,x=31,y=47;bool dead=false,moving=false;void* pMemoryPool=nullptr;void* pDynamicPath=this;D2MissileDataStrc* pMissileData=nullptr;Seed pSeed;};
struct D2GameStrc{int dwGameFrame=1000;}game;struct D2ActiveRoomStrc{}room;
struct D2SkillsTxt{int wAuraTargetState=112;}skill;
struct D2MissilesTxt{int dwParam[2]={4,7},wSubMissile[1]={516};}row;
struct D2MissileStrc{int dwFlags=0,nMissile=0,nSkill=0,nSkillLevel=0,nTargetX=0,nTargetY=0;D2UnitStrc* pOwner=nullptr;D2UnitStrc* pOrigin=nullptr;};
struct D2StatListStrc{int expires=1100;}marker;
struct D2PathPointStrc{uint16_t X=31,Y=47;}point;
struct D2MissileUnitFindArgStrc{D2GameStrc* pGame;D2UnitStrc* pMissile;D2UnitStrc* pOwner;D2MissilesTxt* pMissilesTxtRecord;};
struct Collision{int nCollisionMask=4;void* pfUnitFindCallback=(void*)1;}stru_6FD2E5F8[9];
D2UnitStrc caster,host,emitter,child;bool marked=true,hasCaster=true;int ends=0,scans=0,moves=0,spawns=0,draws=0,pathFlags=8;
D2MissilesTxt* SKILLS_GetMissilesTxtRecord(int){return &row;}D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int){return &skill;}
int MISSILE_GetSkill(D2UnitStrc* m){return m->skill;}int MISSILE_GetLevel(D2UnitStrc* m){return m->rank;}
D2UnitStrc* SUNIT_GetServerUnit(D2GameStrc*,int,int){return hasCaster?&caster:nullptr;}
D2UnitStrc* SUNIT_GetOwner(D2GameStrc*,D2UnitStrc* m){return m==&emitter?&host:&caster;}
bool SUNIT_IsDead(D2UnitStrc* m){return m->dead;}
D2ActiveRoomStrc* UNITS_GetRoom(D2UnitStrc*){return &room;}
int CLIENTS_GetUnitX(D2UnitStrc* m){return m->x;}int CLIENTS_GetUnitY(D2UnitStrc* m){return m->y;}
void D2Common_10229(void*,D2UnitStrc*,D2ActiveRoomStrc*,int,int){}
int ITEMS_RollLimitedRandomNumber(Seed*,int bound){assert(bound==15);draws++;return 7;}
D2StatListStrc* STATLIST_GetStatListFromUnitAndState(D2UnitStrc* m,int state){assert(m==&host&&state==112);return marked?&marker:nullptr;}
int D2COMMON_10473(D2StatListStrc* list){return list->expires;}
int PATH_GetVelocity(void* p){return static_cast<D2UnitStrc*>(p)->moving?960:0;}
int sub_6FCBC930(D2GameStrc*,D2UnitStrc*){moves++;return 1;}
int D2Common_10201(void* p){return static_cast<D2UnitStrc*>(p)->moving?pathFlags:0;}
int UNITS_GetUnitSizeX(D2UnitStrc*){return 1;}
int D2COMMON_10198_PathGetSaveStep(void*,D2PathPointStrc** out){scans++;*out=&point;return 1;}
int COLLISION_CheckMaskWithSize(D2ActiveRoomStrc*,int,int,int,int){return 0;}
D2UnitStrc* D2Common_10407(D2ActiveRoomStrc*,int,int,void*,D2MissileUnitFindArgStrc*,int){std::abort();}
// Complete router ending/null behavior is separately pinned by the router
// probe. No unit contact, fill, HP or child callback is simulated here.
int MISSMODE_SrvDmgHitHandler(D2GameStrc*,D2UnitStrc*,D2UnitStrc* target,int ending){assert(!target&&ending==1);ends++;return 2;}
'''
    common='source/D2Common/src/Units/Missile.cpp';mode='source/D2Game/src/MISSILES/MissMode.cpp'
    for name in ['MISSILE_AllocMissileData','MISSILE_SetTotalFrames','MISSILE_SetCurrentFrame','MISSILE_GetCurrentFrame','MISSILE_GetRemainingFrames','MISSILE_SetActivateFrame','MISSILE_GetActivateFrame','MISSILE_SetTargetX','MISSILE_GetTargetX','MISSILE_SetTargetY','MISSILE_GetTargetY']:
        cpp+=routine(common,name)
    cpp+=r'''
D2UnitStrc* MISSILES_CreateMissileFromParams(D2GameStrc*,D2MissileStrc* args){
 assert(args->dwFlags==2&&args->nMissile==516&&args->pOwner==&caster&&args->pOrigin==&emitter&&args->nSkillLevel==emitter.rank);
 delete child.pMissileData;child={};child.dwClassId=516;child.pDynamicPath=&child;child.rank=args->nSkillLevel;
 MISSILE_AllocMissileData(&child);assert(MISSILE_GetTargetX(&child)==0);spawns++;return &child;
}
'''
    cpp+=routine(mode,'MISSMODE_HandleMissileCollision')+routine(mode,'MISSMODE_SrvDo01_BasicMissile')+routine(mode,'MISSMODE_SrvDo30_RabiesPlague')
    cpp+=r'''
void reset(){delete emitter.pMissileData;delete child.pMissileData;emitter={};child={};caster={};host={};emitter.pDynamicPath=&emitter;child.pDynamicPath=&child;caster.dwUnitType=0;host.dwUnitType=1;MISSILE_AllocMissileData(&emitter);marked=hasCaster=true;host.dead=false;ends=scans=moves=spawns=draws=0;game.dwGameFrame=1000;}
void values(const std::vector<int>& data){printf("[");for(size_t i=0;i<data.size();i++)printf("%s%d",i?",":"",data[i]);printf("]");}
int main(){printf("{\"emitters\":[");bool first=true;
for(int rank=1;rank<=255;rank++)for(int scene=0;scene<5;scene++){
 reset();int total=100+10*(rank-1);emitter.rank=rank;MISSILE_SetTotalFrames(&emitter,total);MISSILE_SetCurrentFrame(&emitter,total);MISSILE_SetTargetX(&emitter,0);MISSILE_SetTargetY(&emitter,1);marker.expires=1000+total;
 std::vector<int> ages,deadlines;int steps=0,result=1;
 while(result!=2&&steps<total+4){steps++;game.dwGameFrame=1000+steps;
  if(scene==1&&steps>=total)marked=false;
  if(scene==2&&steps==total+1)marker.expires=game.dwGameFrame+100;
  if(scene==3&&steps==5)host.dead=true;
  if(scene==4&&steps==5)hasCaster=false;
  int previous=spawns,age=MISSILE_GetRemainingFrames(&emitter);result=MISSMODE_SrvDo30_RabiesPlague(&game,&emitter);
  if(spawns>previous){ages.push_back(age);deadlines.push_back(MISSILE_GetTargetX(&child));}
 }
 printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,",first?"":",",rank,scene,total,steps,result,MISSILE_GetCurrentFrame(&emitter),ends,draws);first=false;values(ages);printf(",");values(deadlines);printf("]");
}
printf("],\"children\":[");first=true;
for(int flags:{0,8,1,4,5}){
 reset();child.pDynamicPath=&child;child.moving=true;MISSILE_AllocMissileData(&child);MISSILE_SetTotalFrames(&child,20);MISSILE_SetCurrentFrame(&child,20);MISSILE_SetActivateFrame(&child,18);pathFlags=flags;
 std::vector<int> eligible;int steps=0,result=1;
 while(result!=2&&steps<24){steps++;int previous=scans;result=MISSMODE_SrvDo01_BasicMissile(&game,&child);if(scans>previous)eligible.push_back(steps);}
 printf("%s[%d,%d,%d,%d,%d,%d,",first?"":",",flags,steps,result,MISSILE_GetCurrentFrame(&child),ends,moves);first=false;values(eligible);printf("]");
}delete emitter.pMissileData;delete child.pMissileData;printf("]}\n");}
'''
    generated=BASE/'druid-rabies-lifecycle-source-probe.cpp';generated.write_text(cpp)
    compiler=BASE/'druid-rabies-lifecycle-compiler.log'
    with compiler.open('w') as log:subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(generated),'-o',str(generated.with_suffix(''))],stdout=log,stderr=subprocess.STDOUT,check=True)
    process=subprocess.run([str(generated.with_suffix(''))],capture_output=True);sanitizer=BASE/'druid-rabies-lifecycle-sanitizer.log';sanitizer.write_bytes(process.stderr)
    assert process.returncode==0 and not process.stderr,process.stderr.decode()[-3000:]
    data=json.loads(process.stdout);assert len(data['emitters'])==1275 and len(data['children'])==5
    for rank,scene,total,steps,result,current,ends,draws,ages,deadlines in data['emitters']:
        stop=5 if scene>=3 else total+2
        expected=list(range(0,4 if scene>=3 else total+1,4))
        assert [steps,result,current,ends,draws,ages]==[stop,2,total-4 if scene>=3 else -1,1 if scene>=3 else 3,2*len(expected),expected]
        assert deadlines==[0 if scene==1 and age+1>=total else 1101+total if scene==2 and age+1>=total+1 else 1000+total for age in ages]
    for flags,steps,result,current,ends,moves,eligible in data['children']:
        bad=bool(flags&5)
        assert [steps,result,current,ends,moves,eligible]==[1 if bad else 20,2,19 if bad else 0,0 if bad else 1,1 if bad else 20,list(range(2,20)) if flags==8 else []]
    fixture=BASE/'druid-rabies-lifecycle-source-probe.json';fixture.write_text(json.dumps(data,separators=(',',':'))+'\n')
    result=dict(referenceVersion='3.3.93847',sourcePreparation=True,currentExecutableParity=False,legacyCommit=tree['sha'],completeRoutineHashes=routines,sourceHashes=hashes,
        emitterContexts=1275,childContexts=5,fixture=dict(path=str(fixture.relative_to(ROOT)),sha256=sha(fixture)),artifacts={str(p.relative_to(ROOT)):sha(p) for p in [generated,compiler,sanitizer]},
        established=[
            'Complete do30 plus generic collision, original getters/setters and zeroing allocator execute across all255ranks and five marker/owner cases. Emitter births observe elapsed ages0,4,etc,including age=total when divisible by4. Generic ending returns are ignored twice; do30returns2at the total+2callback with current=-1. Host death or missing original caster retires on its next callback.',
            'A marker removed before the tail leaves newly allocated child targetXzero through the complete allocator/getter. A replacement marker instead supplies its new absolute deadline while the old emitter retains its original rank/caster. State-list expiration/replacement times are explicit independent inputs,not a claimed modern global event order.',
            'Complete basic child collision decrements20to0, permits ordinary flagged scans at callback2through19, and returns2at20. Bad path flags retire after the first decrement; zero flags suppress scans but retain lifetime. Moving-path service is invoked before each decrement. Real path motion/collision/HP are declared boundaries.',
        ],limits=[
            'World/path/room, movement, stat-list existence/deadline, random offsets, child factory and ending router are declared services. Full allocator/getters/do30/basic/generic collision bodies execute unchanged under ASan/UBSan; the complete router and full relative-origin factory have separate earlier proofs. No actual modern unit-list/event order,ABI,global seed or Fog geometry is certified.',
            'Native migration,independent markers/emitters,tail/replacement ownership,invalid deadline delivery and actual reload acceptance remain separate. No learned Rabies handler or installation is enabled by source preparation.',
        ])
    path=ROOT/'tools/reference/druid-rabies-lifecycle-engine-evidence.json';text=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==text
    else:path.write_text(text)
    print('Rabies lifecycle:1275emitter/5child contexts,14complete routines; independent marker tail/replacement and zero deadline prepared.')


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--check',action='store_true');build(parser.parse_args().check)
