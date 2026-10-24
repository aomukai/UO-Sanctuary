#!/usr/bin/env python3
"""Execute immutable Immolation hit09, integer-disc creator and SOFTHIT callback."""
from pathlib import Path
import argparse,hashlib,json,re,subprocess
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills';p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');args=p.parse_args();tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
rel='source/D2Game/src/MISSILES/MissMode.cpp';raw=(STAGE/'d2moo-pinned-source'/rel).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(i['sha'] for i in tree['tree'] if i['path']==rel);source=raw.decode();blocks={}
def routine(name):
 m=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',source,re.M);assert m,name;e=source.index('{',m.start())+1;depth=1
 while depth:depth+=(source[e]=='{')-(source[e]=='}');e+=1
 body=source[m.start():e];blocks[name]=hashlib.sha256(body.encode()).hexdigest();return body
cpp=r'''#include <algorithm>
#include <cassert>
#include <cstdint>
#include <cstdio>
#include <vector>
#define __fastcall
constexpr int STAT_COLDLENGTH=1,STAT_ITEM_FREEZE=2;
struct D2GameStrc{};struct D2ActiveRoomStrc{};struct D2CoordStrc{int nX=0,nY=0;};
struct D2UnitStrc{int dwClassId=85,x=40,y=70,skill=27,rank=1,pSeed=0,cold=100,freeze=1;};
struct D2MissilesTxt{int dwHitPar[2]={},wHitSubMissile[1]={230},dwHitCalc=100,dwDmgParam[1]={19};unsigned dwHitFlags=2,wResultFlags=5;};
struct D2SkillsTxt{int dwCalc[2]={0,1};};
struct D2MissileStrc{int dwFlags=0,nSkill=0,nSkillLevel=0,nMissile=0,nRange=0,nX=0,nY=0;D2UnitStrc* pOwner=nullptr;};
struct D2DamageStrc{unsigned dwHitFlags=0,wResultFlags=0;};
D2GameStrc game;D2ActiveRoomStrc room;D2UnitStrc parent,owner,child;D2MissilesTxt row;D2SkillsTxt skill;
bool hasRecord=true,hasSkill=true,hasOwner=true;int terrain=0,clears=0,damageCalls=0,areaCalls=0,areaRadius=0,coldAtGetter=0,freezeAtGetter=0,randomValue=0,randomCalls=0;std::vector<D2MissileStrc> created;
D2MissilesTxt* SKILLS_GetMissilesTxtRecord(int){return hasRecord?&row:nullptr;}
D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int){return hasSkill?&skill:nullptr;}
D2UnitStrc* SUNIT_GetOwner(D2GameStrc*,D2UnitStrc*){return hasOwner?&owner:nullptr;}
int MISSILE_GetSkill(D2UnitStrc* p){return p->skill;}int MISSILE_GetLevel(D2UnitStrc* p){return p->rank;}
int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int formula,int id,int rank){assert(id==27 && rank>0);return formula==0?3:4;}
int MISSILE_EvaluateMissileFormula(D2UnitStrc*,D2UnitStrc*,int formula,int id,int){assert(id==85);return formula;}
void STATLIST_SetUnitStat(D2UnitStrc* p,int stat,int value,int){clears++;if(stat==STAT_COLDLENGTH)p->cold=value;else p->freeze=value;}
D2ActiveRoomStrc* UNITS_GetRoom(D2UnitStrc*){return terrain==2?nullptr:&room;}
int CLIENTS_GetUnitX(D2UnitStrc* p){return p->x;}int CLIENTS_GetUnitY(D2UnitStrc* p){return p->y;}
bool COLLISION_RayTrace(D2ActiveRoomStrc*,D2CoordStrc* a,D2CoordStrc* b,unsigned mask){assert(mask==4 && b->nX==2*a->nX-parent.x && b->nY==2*a->nY-parent.y);return terrain==1 && a->nX%2!=0;}
D2ActiveRoomStrc* D2GAME_GetRoom_6FC52070(D2ActiveRoomStrc*,int,int){return &room;}bool DUNGEON_IsRoomInTown(D2ActiveRoomStrc*){return terrain==3;}
D2UnitStrc* MISSILES_CreateMissileFromParams(D2GameStrc*,D2MissileStrc* p){created.push_back(*p);return &child;}
void MISSMODE_GetDamageValue(D2GameStrc*,D2UnitStrc* p,D2UnitStrc*,D2DamageStrc*){damageCalls++;coldAtGetter=p->cold;freezeAtGetter=p->freeze;}
void sub_6FD10200(D2GameStrc*,D2UnitStrc*,int x,int y,int radius,D2DamageStrc* p,int){assert(x==40 && y==70 && p->dwHitFlags==2 && p->wResultFlags==5);areaCalls++;areaRadius=radius;}
unsigned ITEMS_RollRandomNumber(int*){randomCalls++;return randomValue;}
'''
for name in ['MISSMODE_CreateImmolationArrowHitSubmissiles','MISSMODE_SrvHit09_ImmolationArrow','MISSMODE_SrvDmg03_Blaze_FireWall_ImmolationFire_MeteorFire']:cpp+=routine(name)+'\n'
cpp+=r'''int main(){bool first=true;std::printf("{\"hit\":[");
for(int exists:{0,1})for(int record:{0,1})for(int skillExists:{0,1})for(int own:{0,1})for(int sub:{-1,0,230})for(int disc:{0,2,3})for(int area:{0,4,5})for(int duration:{-2,0,100})for(int world:{0,1,2,3})for(int rank:{1,20}){
 parent={};parent.rank=rank;row={};row.wHitSubMissile[0]=sub;row.dwHitPar[0]=disc;row.dwHitPar[1]=area;row.dwHitCalc=duration;hasRecord=record;hasSkill=skillExists;hasOwner=own;terrain=world;clears=damageCalls=areaCalls=areaRadius=0;coldAtGetter=freezeAtGetter=-1;created.clear();
 int result=MISSMODE_SrvHit09_ImmolationArrow(&game,exists?&parent:nullptr,nullptr);if(!first)std::printf(",");first=false;
 std::printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,[",exists,record,skillExists,own,sub,disc,area,duration,world,rank,result,clears,damageCalls,areaCalls,areaRadius,coldAtGetter,freezeAtGetter);
 for(size_t i=0;i<created.size();i++){auto& m=created[i];std::printf("%s[%d,%d,%d,%d,%d,%d,%d,%d]",i?",":"",m.nX,m.nY,m.nRange,m.dwFlags,m.nSkill,m.nSkillLevel,m.pOwner==&owner,m.nMissile);}std::printf("]]");
}
std::printf("],\"soft\":[");first=true;
for(int exists:{0,1})for(int record:{0,1})for(int roll=0;roll<128;roll++)for(int chance:{0,19,128})for(int initial:{0,5}){
 hasRecord=record;row.dwDmgParam[0]=chance;randomValue=roll;randomCalls=0;D2DamageStrc damage;damage.wResultFlags=initial;MISSMODE_SrvDmg03_Blaze_FireWall_ImmolationFire_MeteorFire(&game,exists?&parent:nullptr,nullptr,&damage);
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%u,%d]",exists,record,roll,chance,initial,damage.wResultFlags,randomCalls);
}std::printf("]}\n");}
'''
path=STAGE/'immolation-arrow-source-probe.cpp';path.write_text(cpp);binary=STAGE/'immolation-arrow-source-probe';subprocess.run(['g++','-std=c++20','-O2',str(path),'-o',str(binary)],check=True)
data=json.loads(subprocess.check_output([str(binary)]));assert len(data['hit'])==10368 and len(data['soft'])==3072
for exists,record,skillExists,own,sub,disc,area,duration,world,rank,result,clears,damage,areaCalls,r,cold,freeze,created in data['hit']:
 active=bool(exists and record and skillExists);spawns=active and sub>=0;assert result==(3 if active else 1) and clears==2*spawns and damage==areaCalls==int(active) and r==((area if area>0 else 4) if active else 0)
 assert(cold,freeze)==((0,0) if spawns else (100,1) if active else (-1,-1))
 radius=disc if disc>0 else 3;points=[(40+x,70+y) for x in range(-radius,radius+1) for y in range(-radius,radius+1) if x*x+y*y<=radius*radius and (world!=1 or (40+x)%2==0)] if spawns and world not in(2,3) else []
 assert[(m[0],m[1]) for m in created]==points
 assert all(m[2:]==[max(duration,0),0x8001 if duration>0 else 1,27,rank,own,sub] for m in created)
for exists,record,roll,chance,initial,flags,draws in data['soft']:assert flags==initial|(0x4000 if exists and record and roll<chance else 0) and draws==int(bool(exists and record))
fixture=STAGE/'immolation-arrow-source-probe.json';fixture.write_text(json.dumps(data,separators=(',',':'))+'\n')
e=dict(referenceVersion='3.3.93847',talentId='amazon.immolationarrow',handlerEnabled=False,legacyCommit=tree['sha'],sourceHashes={rel:hashlib.sha256(raw).hexdigest()},compiledRoutines=list(blocks),blockHashes=blocks,probe=dict(hitDiscCases=10368,softHitCases=3072,path=str(fixture.relative_to(ROOT)),sha256=hashlib.sha256(fixture.read_bytes()).hexdigest()),established=['Complete hit09 clears parent cold/item-freeze stats before ground emission and its one damage-getter/area dispatch. Missing missile/row/skill exits; current sub230 uses radius3/duration100 and radius4 explosion. Owner/skill/rank and source callbacks remain distinct.','Complete integer-disc maker emits29 eligible subtile cells for radius3 in the unblocked world, in x/y iteration order. Ray endpoints are candidate and twice its offset from the parent; declared walls/rooms/town remove individual cells. Positive duration clamps/flags1|0x8000 and original skill/rank/owner are preserved.','Complete srvdmg3 draws one source random number for a valid missile/row and ORs SOFTHIT0x4000 when low7bits are below19. Existing result flags remain intact.'],limits=['Damage getter/area dispatch, world room/town/collision, factory allocation, formulas/stats and RNG are declared services. This compiles the complete specific callbacks/disc body, not a full modern factory/collision/damage/owner/geometry implementation. Null-owner matrix rows record callback arguments; downstream factory/area rejection is not simulated as actual damage.','Generic direct-hit result routing, parent/field creator channels, localdelay/payment, do5 field per-frame collision, DamageRate41 and full modern event/stat/seed/quiver/item/owner/client behavior still require proof. No handler or substitute field is enabled.'])
out=ROOT/'tools/reference/immolation-arrow-engine-evidence.json';encoded=json.dumps(e,indent=2)+'\n'
if args.check:assert out.read_text()==encoded,'Immolation Arrow evidence stale'
else:out.write_text(encoded)
print('Immolation Arrow:10368 complete hit/disc and3072 SOFTHIT callback cases compiled; handler disabled.')
