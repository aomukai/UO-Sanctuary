#!/usr/bin/env python3
"""Execute the complete generic missile collision/update body used by poison."""
from pathlib import Path
import argparse,hashlib,json,re,subprocess
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills';parser=argparse.ArgumentParser();parser.add_argument('--check',action='store_true');args=parser.parse_args()
tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
rel='source/D2Game/src/MISSILES/MissMode.cpp';raw=(STAGE/'d2moo-pinned-source'/rel).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel)
text=raw.decode();name='MISSMODE_HandleMissileCollision';m=re.search(r'^int32_t __fastcall '+name+r'\([^\n]+\)\s*\n\{',text,re.M);assert m;e=text.index('{',m.start())+1;depth=1
while depth:depth+=(text[e]=='{')-(text[e]=='}');e+=1
body=text[m.start():e]
cpp=r'''#include <cstdint>
#include <cstdio>
#include <initializer_list>
#define __fastcall
constexpr int MISSMODE_NOCOLLIDE=0,MISSMODE_BARRIERCOLLIDE=1;
struct D2GameStrc{};struct D2ActiveRoomStrc{};struct D2DynamicPathStrc{int velocity=0,flags=64;};struct D2PathPointStrc{uint16_t X=10,Y=20;};
struct D2UnitStrc{int dwClassId=39,dwMissileMode=3,frame=60,activation=0;D2DynamicPathStrc* pDynamicPath=nullptr;};struct D2MissilesTxt{};
struct D2MissileUnitFindArgStrc{D2GameStrc* pGame;D2UnitStrc* pMissile;D2UnitStrc* pOwner;D2MissilesTxt* pMissilesTxtRecord;};
using Finder=int(*)(void);int findCallback(){return 0;}struct Route{Finder pfUnitFindCallback;int nCollisionMask;};Route stru_6FD2E5F8[4]={{nullptr,0},{nullptr,4},{nullptr,0},{findCallback,68}};
D2ActiveRoomStrc room;D2MissilesTxt row;D2UnitStrc victim,owner;D2PathPointStrc points[2];bool hasRow=true,hasRoom=true,hasTarget=true;int moveResult=1,steps=1,mask=64,moves,finds,hits,lastEnd,frameWrites;
D2MissilesTxt* SKILLS_GetMissilesTxtRecord(int){return hasRow?&row:nullptr;}
int PATH_GetVelocity(D2DynamicPathStrc* p){return p?p->velocity:0;}int sub_6FCBC930(D2GameStrc*,D2UnitStrc*){moves++;return moveResult;}
int MISSILE_GetCurrentFrame(D2UnitStrc* p){return p->frame;}void MISSILE_SetCurrentFrame(D2UnitStrc* p,int n){frameWrites++;p->frame=n;}int MISSILE_GetActivateFrame(D2UnitStrc* p){return p->activation;}
D2ActiveRoomStrc* UNITS_GetRoom(D2UnitStrc*){return hasRoom?&room:nullptr;}int D2Common_10201(D2DynamicPathStrc* p){return p->flags;}
D2UnitStrc* SUNIT_GetOwner(D2GameStrc*,D2UnitStrc*){return &owner;}int UNITS_GetUnitSizeX(D2UnitStrc*){return 2;}
int D2COMMON_10198_PathGetSaveStep(D2DynamicPathStrc*,D2PathPointStrc** p){*p=points;return steps;}
int COLLISION_CheckMaskWithSize(D2ActiveRoomStrc*,int,int,int,int){return mask;}
D2UnitStrc* D2Common_10407(D2ActiveRoomStrc*,int,int,Finder,D2MissileUnitFindArgStrc*,int){finds++;return hasTarget?&victim:nullptr;}
int MISSMODE_SrvDmgHitHandler(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,int end){hits++;lastEnd=end;return 7;}
'''+body+r'''
int main(){D2GameStrc game;D2DynamicPathStrc path;D2UnitStrc missile;bool first=true;std::printf("[");
for(int frame:{0,1,2,60})for(int velocity:{0,24})for(int move:{1,2})for(int mode:{0,1,2,3})for(int flags:{0,1,2,4,64})for(int activate:{0,100})for(int target:{0,1})for(int collision:{0,4,64,68}){
 missile={};missile.pDynamicPath=&path;missile.frame=frame;missile.dwMissileMode=mode;missile.activation=activate;path.velocity=velocity;path.flags=flags;moveResult=move;hasTarget=target;mask=collision;moves=finds=hits=frameWrites=0;lastEnd=-1;
 int result=MISSMODE_HandleMissileCollision(&game,&missile);if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",frame,velocity,move,mode,flags,activate,target,collision,result,missile.frame,moves,finds,hits,lastEnd,frameWrites);
}std::printf("]\n");}
'''
p=STAGE/'poison-collision-source-probe.cpp';p.write_text(cpp);binary=STAGE/'poison-collision-source-probe';subprocess.run(['g++','-std=c++20','-O2',str(p),'-o',str(binary)],check=True);data=json.loads(subprocess.check_output([str(binary)]));assert len(data)==5120
for frame,velocity,move,mode,flags,activate,target,mask,result,outFrame,moves,finds,hits,end,writes in data:
 early=bool(velocity and move==2);expire=not early and frame<=1;active=not(early or expire or mode==0 or flags&5 or mode==1 or frame-1>activate or not flags or mode==2)
 expected=7 if early or expire else 1 if mode==0 else 2 if flags&5 else 1 if mode==1 or frame-1>activate or not flags else 2 if mode==2 else 7 if mask and target else 2 if mask&4 else 1
 assert(result,outFrame,moves,writes)==(expected,frame if early else frame-1,int(bool(velocity)),int(not early))
 assert finds==int(active and mask!=0) and hits==int(bool(early or expire or active and(mask&4 or mask and target)))
 if hits:assert end==int(early or expire or not target)
fixture=STAGE/'poison-collision-source-probe.json';fixture.write_text(json.dumps(data,separators=(',',':'))+'\n')
e=dict(referenceVersion='3.3.93847',handlerEnabled=False,legacyCommit=tree['sha'],sourceHashes={rel:hashlib.sha256(raw).hexdigest()},compiledRoutines=[name],blockHashes={name:hashlib.sha256(body.encode()).hexdigest()},probe=dict(cases=len(data),path=str(fixture.relative_to(ROOT)),sha256=hashlib.sha256(fixture.read_bytes()).hexdigest()),established=['Full do-collision body attempts moving-path advancement before lifetime decrement. A failed moving-path result ends through the damage handler without decrementing the frame. Otherwise it decrements once before contact selection, and frame<=0 ends before contacts. A stationary60-frame cloud therefore has59 possible contact passes, followed by its ending callback.','NOCOLLIDE returns after lifetime decrement. Collision flags1or4 remove; barrier mode, not-yet-activated frame or zero flags skip contact. Missing route callback removes. Active saved steps use one candidate finder; first candidate immediately dispatches one hit handler. No candidate plus wallbit4 dispatches an ending handler and returns removal2.','Trail do2 invokes its creator before this entire body (existing complete trail proof). Thus a cloud may be created from the previous path-change flag before the next move, including the parent final update; this is not an every-cell emission loop.'],limits=['World/path advancement, room masks, saved steps, route callback, owner, candidate finder and final hit/removal handler are declared services. These5120 cases cover valid missile/row/path/room with one saved step; null/invalid input, full path computation, actual finder spatial/ordering, outer event/removal and modern executable equivalence remain separate.','Combined source proofs establish packet/factory/router/update contracts; no Poison Javelin handler is enabled by this fixture. Native trail geometry, do-handler integration, start/release resources, item events and actual input/reload remain to implement.'])
out=ROOT/'tools/reference/poison-collision-engine-evidence.json';encoded=json.dumps(e,indent=2)+'\n'
if args.check:assert out.read_text()==encoded,'Poison collision evidence stale'
else:out.write_text(encoded)
print('Poison collision:5120 complete update/lifetime/activation/contact/end cases compiled; world/path/finder adapters remain explicit.')
