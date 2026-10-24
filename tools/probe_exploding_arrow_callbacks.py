#!/usr/bin/env python3
"""Compile unchanged parent/child explosion and per-recipient area callbacks."""
from pathlib import Path
import argparse,hashlib,json,re,subprocess
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills';parser=argparse.ArgumentParser();parser.add_argument('--check',action='store_true');args=parser.parse_args()
tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416';hashes={};blocks={}
def routine(rel,name):
 raw=(STAGE/'d2moo-pinned-source'/rel).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel)
 hashes[rel]=hashlib.sha256(raw).hexdigest();text=raw.decode();m=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',text,re.M);assert m,name;end=text.index('{',m.start())+1;depth=1
 while depth:depth+=(text[end]=='{')-(text[end]=='}');end+=1
 body=text[m.start():end];blocks[name]=hashlib.sha256(body.encode()).hexdigest();return body
mode='source/D2Game/src/MISSILES/MissMode.cpp';skills='source/D2Game/src/SKILLS/Skills.cpp';ass='source/D2Game/src/SKILLS/SkillAss.cpp'
cpp=r'''#include <algorithm>
#include <cstdint>
#include <cstdio>
#include <cstring>
#include <vector>
#define __fastcall
constexpr int UNIT_MISSILE=3,STATE_UNINTERRUPTABLE=1,DAMAGERESULTFLAG_SUCCESSFULHIT=1;
struct D2UnitStrc{int dwClassId=41,dwUnitType=0,skill=16,rank=1,x=40,y=70,block=0;bool uninterruptible=false;};
struct D2GameStrc{};struct D2MissilesTxt{int dwHitPar[4]={};int wHitSubMissile[4]={};uint32_t dwHitFlags=2;uint16_t wResultFlags=5;};
struct D2SkillsTxt{int dwCalc[4]={};};
struct D2DamageStrc{uint32_t dwHitFlags=0;uint16_t wResultFlags=0;int dwPhysDamage=512,dwFireDamage=769;};
struct D2MissileStrc{D2UnitStrc* pOwner=nullptr;D2UnitStrc* pOrigin=nullptr;int nSkill=0,nSkillLevel=0,nMissile=0;};
struct D2AuraCallbackStrc{D2GameStrc* pGame;D2UnitStrc* pOwner;void* pArgs;};
D2GameStrc game;D2UnitStrc parent,child,owner,primary,targets[3];D2MissilesTxt parentRow,childRow;D2SkillsTxt skill;
bool hasRecord=true,hasOwner=true,failCreate=false,hasSkill=true;int creates=0,created=0,immediate=0,damageCalls=0,areaCalls=0,formulaCalls=0,calc=1,recipients=1,areaRadius=0,areaX=0,areaY=0;uint32_t areaFilter=0;
D2MissileStrc captured;std::vector<int> packets,events;
D2MissilesTxt* SKILLS_GetMissilesTxtRecord(int id){return !hasRecord?nullptr:id==41?&parentRow:&childRow;}
D2UnitStrc* SUNIT_GetOwner(D2GameStrc*,D2UnitStrc*){return hasOwner?&owner:nullptr;}
int MISSILE_GetSkill(D2UnitStrc* u){return u->skill;}int MISSILE_GetLevel(D2UnitStrc* u){return u->rank;}
D2UnitStrc* MISSILES_CreateMissileFromParams(D2GameStrc*,D2MissileStrc* p){creates++;captured=*p;if(failCreate)return nullptr;created++;return &child;}
int MISSMODE_SrvDmgHitHandler(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,int){immediate++;return 1;}
D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int){return hasSkill?&skill:nullptr;}
int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int,int,int){formulaCalls++;return calc;}
int CLIENTS_GetUnitX(D2UnitStrc* p){return p->x;}int CLIENTS_GetUnitY(D2UnitStrc* p){return p->y;}
void MISSMODE_GetDamageValue(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,D2DamageStrc* p){damageCalls++;p->dwPhysDamage=512;p->dwFireDamage=769;}
int SUNITDMG_ApplyBlockOrDodge(D2GameStrc*,D2UnitStrc*,D2UnitStrc* t,int a,int b){return t->block;}
bool STATES_CheckState(D2UnitStrc* t,int){return t->uninterruptible;}
void D2GAME_MONSTER_ApplyCriticalDamage_6FC62E70(D2UnitStrc*,D2UnitStrc*,D2DamageStrc*){}
void SUNITDMG_ExecuteEvents(D2GameStrc*,D2UnitStrc*,D2UnitStrc* t,int,D2DamageStrc*){events.push_back(t->dwClassId);}
void SUNITDMG_ExecuteMissileDamage(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,D2DamageStrc* p){packets.push_back(p->wResultFlags);packets.push_back(p->dwHitFlags);packets.push_back(p->dwPhysDamage);packets.push_back(p->dwFireDamage);p->dwPhysDamage=-1234;}
void sub_6FD0FE80(D2GameStrc* g,D2UnitStrc* o,int x,int y,int radius,uint32_t filter,int(*callback)(D2AuraCallbackStrc*,D2UnitStrc*),void* args,int,const char*,int){areaCalls++;areaX=x;areaY=y;areaRadius=radius;areaFilter=filter;D2AuraCallbackStrc aura{g,o,args};for(int i=0;i<recipients;i++)callback(&aura,&targets[i]);}
'''
cpp+=routine(skills,'sub_6FD10140')+'\n'+routine(ass,'sub_6FCF5DE0')+'\n'+routine(skills,'sub_6FD10200')+'\n'+routine(mode,'MISSMODE_SrvHit04_ExplodingArrow_FreezingArrow_RoyalStrikeMeteorCenter')+'\n'+routine(mode,'MISSMODE_SrvHit01_Fireball_ExplodingArrow_FreezingArrowExplosion')+r'''
int main(){bool first=true;std::printf("{\"parent\":[");
for(int exists:{0,1})for(int record:{0,1})for(int own:{0,1})for(int param:{0,1})for(int fail:{0,1})for(int set:{0,1,2,3})for(int rank:{1,20}){
 parent={};parent.dwClassId=41;parent.rank=rank;parentRow={};for(int i=0;i<(set==3?4:set);i++)parentRow.wHitSubMissile[i]=656+i;parentRow.dwHitPar[0]=param;hasRecord=record;hasOwner=own;failCreate=fail;creates=created=immediate=0;captured={};
 int result=MISSMODE_SrvHit04_ExplodingArrow_FreezingArrow_RoyalStrikeMeteorCenter(&game,exists?&parent:nullptr,&primary);
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",exists,record,own,param,fail,set,rank,result,creates,created,immediate,captured.nSkill,captured.nSkillLevel,captured.pOwner==&owner,captured.pOrigin==&parent);
}
std::printf("],\"child\":[");first=true;
for(int exists:{0,1})for(int record:{0,1})for(int own:{0,1})for(int radius:{-2,0,5})for(int skillExists:{0,1})for(int formula:{-1,0,7})for(int count:{1,3})for(int block:{0,1,2,4,16})for(int flags:{0,1,5}){
 child={};child.dwClassId=656;child.x=40;child.y=70;childRow={};childRow.dwHitPar[0]=radius;childRow.wResultFlags=flags;childRow.dwHitFlags=2;hasRecord=record;hasOwner=own;hasSkill=skillExists;calc=formula;recipients=count;damageCalls=areaCalls=formulaCalls=areaRadius=areaX=areaY=0;areaFilter=0;packets.clear();events.clear();
 for(int i=0;i<3;i++){targets[i]={};targets[i].dwClassId=100+i;targets[i].block=block;}
 int result=MISSMODE_SrvHit01_Fireball_ExplodingArrow_FreezingArrowExplosion(&game,exists?&child:nullptr,&primary);
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%u,%d,%d,[",exists,record,own,radius,skillExists,formula,count,block,flags,result,damageCalls,areaCalls,formulaCalls,areaRadius,areaFilter,areaX,areaY);
 for(size_t i=0;i<packets.size();i++)std::printf("%s%d",i?",":"",packets[i]);std::printf("],%zu]",events.size());
}
std::printf("]}\n");}
'''
path=STAGE/'exploding-arrow-callback-source-probe.cpp';path.write_text(cpp);binary=STAGE/'exploding-arrow-callback-source-probe';subprocess.run(['g++','-std=c++20','-O2',str(path),'-o',str(binary)],check=True)
data=json.loads(subprocess.check_output([str(binary)]));assert len(data['parent'])==256 and len(data['child'])==4320,(len(data['parent']),len(data['child']))
for row in data['parent']:
 exists,record,own,param,fail,set,rank,result,attempts,created,immediate,skill,level,owner,origin=row
 expected=(4 if set==3 else set) if exists and record else 0
 assert result==(3 if exists and record else 1) and attempts==expected and created==expected*(not fail) and immediate==created*param
 assert (skill,level,owner,origin)==((16,rank,own,1) if expected else (0,0,0,0))
for row in data['child']:
 exists,record,own,radius,skillExists,formula,count,block,flags,result,damage,area,formulas,r,filter,x,y,packets,events=row
 accepted=bool(exists and record and own and (radius>0 or skillExists));assert result==1 and damage==area==int(accepted)
 if accepted:
  assert formulas==int(radius<=0)
  assert r==(radius if radius>0 else max(1,formula)) and filter==0x8583 and (x,y)==(40,70)
  assert len(packets)==(count*4 if flags else 0) and all(packets[i+2:i+4]==[512,769] for i in range(0,len(packets),4))
  assert events==(count if flags&1 and not block else 0)
fixture=STAGE/'exploding-arrow-callback-source-probe.json';fixture.write_text(json.dumps(data,separators=(',',':'))+'\n')
e=dict(referenceVersion='3.3.93847',handlerEnabled=False,legacyCommit=tree['sha'],sourceHashes=hashes,compiledRoutines=list(blocks),blockHashes=blocks,probe=dict(parentCases=256,childAreaCases=4320,path=str(fixture.relative_to(ROOT)),sha256=hashlib.sha256(fixture.read_bytes()).hexdigest()),established=['Complete parent hit4 copies original owner/origin/skill/rank into each positive HitSubMissile creation attempt. Optional immediate child hit dispatch occurs only after successful creation and positive parent sHitPar1; current Exploding Arrow row has0.','Complete child hit1 obtains one declared damage packet, adds row flags and invokes the shared area delivery at child coordinates. Positive row radius overrides skill formula; nonpositive row radius evaluates a clamped minimum1 skill radius. Missing missile/record/owner/required skill exits without damage.','Complete area delivery uses default filter0x8583. Each recipient callback copies the original prepared packet before complete per-target block/dodge/result/event/missile routing. A deliberately destructive downstream stub cannot contaminate the next recipient; flagged successful/unblocked events remain individual.'],limits=['World recipient enumeration, formula/stat/damage getter, factory allocation, critical and final damage/event services are explicit adapters. This establishes callback ordering/data flow, not modern collision/factory/damage channel inheritance or live-engine equivalence.','Parent versus child physical/item-fire/skill-fire, weapon events, source sentinels, creation-time versus impact-time modifiers, wall/lifetime/AlwaysExplode/child scheduling/Pierce and actual current behavior remain to establish before a handler is enabled. No independent per-recipient damage reroll is introduced by the compiled callbacks.'])
out=ROOT/'tools/reference/exploding-arrow-callback-engine-evidence.json';encoded=json.dumps(e,indent=2)+'\n'
if args.check:assert out.read_text()==encoded,'Explosion callback evidence stale'
else:out.write_text(encoded)
print('Exploding Arrow:256 parent and4320 child/area callback cases compiled from unchanged bodies; no handler enabled.')
