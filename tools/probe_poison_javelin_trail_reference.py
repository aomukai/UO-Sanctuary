#!/usr/bin/env python3
"""Compile full Poison Javelin trail/path gate and stationary do3 callbacks."""
from pathlib import Path
import argparse,hashlib,json,re,subprocess
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills';p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');args=p.parse_args();tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());hashes={};blocks={}
def routine(rel,name):
 raw=(STAGE/'d2moo-pinned-source'/rel).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(i['sha'] for i in tree['tree'] if i['path']==rel);hashes[rel]=hashlib.sha256(raw).hexdigest();text=raw.decode();m=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',text,re.M);assert m,name;e=text.index('{',m.start())+1;depth=1
 while depth:depth+=(text[e]=='{')-(text[e]=='}');e+=1
 body=text[m.start():e];blocks[name]=hashlib.sha256(body.encode()).hexdigest();return body
mode='source/D2Game/src/MISSILES/MissMode.cpp';path='source/D2Common/src/Path/Step.cpp'
cpp=r'''#include <cstdint>
#include <cstdio>
#include <initializer_list>
#define __fastcall
#define __stdcall
using BOOL=int;constexpr int FALSE=0,PATH_UNKNOWN_FLAG_0x00008=8;
struct D2DynamicPathStrc{int dwFlags=8,velocity=24;};struct D2GameStrc{};struct D2ActiveRoomStrc{};
struct D2UnitStrc{int dwClassId=38,level=1;D2DynamicPathStrc* pDynamicPath=nullptr;};
struct D2MissilesTxt{int wSubMissile[1]={39},dwSrvCalc=0;};
struct D2MissileStrc{int dwFlags=0,nRange=0,nLoops=0;D2UnitStrc* pOwner=nullptr;D2UnitStrc* pOrigin=nullptr;int nMissile=0,nX=0,nY=0,nSkill=0,nSkillLevel=0;};
D2UnitStrc owner,parent;D2ActiveRoomStrc room;D2MissilesTxt row;D2MissileStrc captured;bool hasRow=true,hasOwner=true;int calc=0,created=0,collision=0,masks=0;
D2MissilesTxt* SKILLS_GetMissilesTxtRecord(int){return hasRow?&row:nullptr;}D2UnitStrc* SUNIT_GetOwner(D2GameStrc*,D2UnitStrc*){return hasOwner?&owner:nullptr;}
int MISSILE_GetLevel(D2UnitStrc* u){return u->level;}int MISSILE_GetSkill(D2UnitStrc*){return 15;}int CLIENTS_GetUnitX(D2UnitStrc*){return 40;}int CLIENTS_GetUnitY(D2UnitStrc*){return 70;}
int MISSILE_EvaluateMissileFormula(D2UnitStrc*,D2UnitStrc*,int,int,int){return calc;}
D2UnitStrc* MISSILES_CreateMissileFromParams(D2GameStrc*,D2MissileStrc* args){created++;captured=*args;return &parent;}
int MISSMODE_HandleMissileCollision(D2GameStrc*,D2UnitStrc*){collision++;return 42;}int PATH_GetVelocity(D2DynamicPathStrc* p){return p->velocity;}
D2ActiveRoomStrc* UNITS_GetRoom(D2UnitStrc*){return &room;}void COLLISION_SetMask(D2ActiveRoomStrc*,int,int,int mask){if(mask==0x40)masks++;}
'''
for rel,name in [(path,'D2Common_10234'),(mode,'MISSMODE_CreatePlagueJavelin_PoisonJavelin_PoisonTrapHitSubmissiles'),(mode,'MISSMODE_SrvDo02_PlagueJavelin_PoisonJavelin_PoisonTrap'),(mode,'MISSMODE_SrvDo03_PoisonCloud_Blizzard_ThunderStorm_HandOfGod')]:cpp+=routine(rel,name)+'\n'
cpp+=r'''
int main(){D2GameStrc game;D2DynamicPathStrc path;bool first=true;std::printf("{\"parent\":[");
for(int present:{0,1})for(int record:{0,1})for(int child:{-1,39})for(int own:{0,1})for(int hasPath:{0,1})for(int flags:{0,8,16,24})for(int level:{1,2,20})for(int formula:{0,3}){
 parent={};parent.level=level;parent.pDynamicPath=hasPath?&path:nullptr;path.dwFlags=flags;hasRow=record;hasOwner=own;row.wSubMissile[0]=child;calc=formula;created=collision=0;captured={};int result=MISSMODE_SrvDo02_PlagueJavelin_PoisonJavelin_PoisonTrap(&game,present?&parent:nullptr);
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",present,record,child,own,hasPath,flags,level,formula,result,collision,created,captured.dwFlags,captured.nRange,captured.nLoops,captured.nSkill,captured.nSkillLevel);
}
std::printf("],\"creator\":[");first=true;
for(int flags:{0,8})for(int own:{0,1})for(int range:{-1,0,60})for(int loops:{-1,0,3})for(int level:{1,2,20}){
 parent={};parent.level=level;parent.pDynamicPath=&path;path.dwFlags=flags;hasOwner=own;created=0;captured={};auto* result=MISSMODE_CreatePlagueJavelin_PoisonJavelin_PoisonTrapHitSubmissiles(&game,&parent,39,range,loops);
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",flags,own,range,loops,level,result!=nullptr,created,captured.dwFlags,captured.nRange,captured.nLoops);
}
std::printf("],\"cloud\":[");first=true;
for(int hasPath:{0,1})for(int velocity:{0,24}){parent={};parent.pDynamicPath=hasPath?&path:nullptr;path.velocity=velocity;masks=collision=0;int result=MISSMODE_SrvDo03_PoisonCloud_Blizzard_ThunderStorm_HandOfGod(&game,&parent);if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d]",hasPath,velocity,result,masks,collision);}std::printf("]}\n");}
'''
p=STAGE/'poison-javelin-trail-source-probe.cpp';p.write_text(cpp);binary=STAGE/'poison-javelin-trail-source-probe';subprocess.run(['g++','-std=c++20','-O2',str(p),'-o',str(binary)],check=True);data=json.loads(subprocess.check_output([str(binary)]));assert len(data['parent'])==768 and len(data['creator'])==108 and len(data['cloud'])==4
for present,record,child,owner,path,flags,level,formula,result,collision,created,childFlags,range,loops,skill,rank in data['parent']:
 ok=bool(present and record and child>=0 and owner and path and flags&8);assert(result,collision,created)==(42,1,int(ok));assert(childFlags,range,loops,skill,rank)==((5|(8 if formula>0 else 0),0,2*(level-1),15,level) if ok else(0,0,0,0,0))
for flags,owner,range,loops,level,result,created,outFlags,outRange,outLoops in data['creator']:
 ok=bool(flags&8 and owner);assert result==created==int(ok);assert(outFlags,outRange,outLoops)==((5|(0x8000 if range>0 else 0)|(8 if loops>0 else 0),max(0,range),2*(level-1)) if ok else(0,0,0))
for path,velocity,result,masks,collision in data['cloud']:assert(result,masks,collision)==(42,int(not path or not velocity),1)
fixture=STAGE/'poison-javelin-trail-source-probe.json';fixture.write_text(json.dumps(data,separators=(',',':'))+'\n')
e=dict(referenceVersion='3.3.93847',talentId='amazon.poisonjavelin',handlerEnabled=False,legacyCommit=tree['sha'],sourceHashes=hashes,compiledRoutines=list(blocks),blockHashes=blocks,probe=dict(parentCases=768,creatorCases=108,cloudCases=4,path=str(fixture.relative_to(ROOT)),sha256=hashlib.sha256(fixture.read_bytes()).hexdigest()),established=['Complete parent do2 calls trail creation before generic collision, including its final collision update. Full10234 only accepts dynamic paths with flag0x8; null path or merely another flag cannot emit a cloud. Exact outer path flag-setting cadence/positions remain to trace.','Complete child creator uses current parent coordinates, original owner/origin/skill/rank, flags5 and optional range/loop flags. It overwrites nLoops with2*(rank-1) even when current formula0 leaves loop flag8 absent; the full factory must establish whether that unflagged value affects actual lifetime.','Complete child do3 writes0x40 only for no-path/zero-velocity fields, then delegates full collision. This does not by itself establish recipient footprint, damage type, lifetime or repeat-poison application.'],limits=['Formula, generic collision, actual missile factory/stat inheritance, owner lookup and room/world masks are declared services. Full path flag-setting, creator SrcDamage-1 and getter, damage/rate/length/count and current executable geometry/seed/owner/client behavior remain outside this proof.','No Poison Javelin handler or substitute generic green cloud is enabled. These older complete bodies prepare the next Amazon skill; current tables remain the mechanical pin.'])
out=ROOT/'tools/reference/poison-javelin-trail-engine-evidence.json';encoded=json.dumps(e,indent=2)+'\n'
if args.check:assert out.read_text()==encoded,'Poison Javelin trail evidence stale'
else:out.write_text(encoded)
print('Poison Javelin:768 parent/path,108 creator flag/rank and4 stationary cloud callbacks compiled; damage/factory/collision pending.')
