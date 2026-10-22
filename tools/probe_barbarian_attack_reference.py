#!/usr/bin/env python3
"""Compile unchanged start32 and combat allocation; isolate postmitigation flat addition."""
from pathlib import Path
import hashlib,json,subprocess
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills';BASE=STAGE/'d2moo-pinned-source'
tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416' and not tree['truncated']
entries={r['path']:r['sha'] for r in tree['tree']}
paths=['source/D2Game/src/SKILLS/SkillBar.cpp','source/D2Game/src/UNIT/SUnitDmg.cpp','source/D2Game/src/SKILLS/Skills.cpp','source/D2Common/src/D2Skills.cpp']
for path in paths:
 data=(BASE/path).read_bytes();assert hashlib.sha1(f'blob {len(data)}\0'.encode()+data).hexdigest()==entries[path]
def routine(path,name):
 text=(BASE/path).read_text();at=text.index('__fastcall '+name+'(');start=text.rfind('\n',0,at)+1;end=text.index('{',at)+1;depth=1
 while depth:depth+=(text[end]=='{')-(text[end]=='}');end+=1
 return text[start:end]
start=routine(paths[0],'SKILLS_SrvSt32_Conversion_Bash_Stun_Concentrate_BearSmite')
alloc=routine(paths[1],'SUNITDMG_AllocCombat')
text=(BASE/paths[1]).read_text();at=text.index('\tif (pDamage->dwDmgTotal > 0)');end=text.index('{',at)+1;depth=1
while depth:depth+=(text[end]=='{')-(text[end]=='}');end+=1
health=text[at:end]

cpp=r'''#include <cstdint>
#include <cstdio>
#include <cstring>
#include <initializer_list>
#define __fastcall
constexpr int DAMAGERESULTFLAG_SUCCESSFULHIT=1,DAMAGERESULTFLAG_DODGE=2,DAMAGERESULTFLAG_AVOID=4,DAMAGERESULTFLAG_EVADE=8,DAMAGERESULTFLAG_WEAPONBLOCK=16,DAMAGERESULTFLAG_WILLDIE=32;
constexpr int DAMAGEHITFLAG_2=2,UNIT_MONSTER=1,UNIT_PLAYER=0,STAT_HITPOINTS=0;
struct D2DamageStrc{int wResultFlags=0,dwHitFlags=0,dwHitClass=0,dwEnDmgPct=0,dwConvPct=0,nConvType=0,dwPhysDamage=0,dwFireDamage=0,dwLtngDamage=0,dwMagDamage=0,dwColdDamage=0,dwPoisDamage=0,dwLifeLeech=0,dwDmgTotal=0;};
struct D2GameStrc{void* pMemoryPool=nullptr;};
struct D2CombatStrc;
struct D2UnitStrc{int dwUnitType=0,dwUnitId=1,hp=10000000;D2CombatStrc* pCombat=nullptr;};
struct D2CombatStrc{D2GameStrc* pGame;int dwAttackerType,dwAttackerId,dwDefenderType,dwDefenderId;D2DamageStrc tDamage;D2CombatStrc* pNext;};
struct D2SkillsTxt{int dwCalc[4]={0,1,2,3},wResultFlags=0,dwHitFlags=0,dwHitClass=0,nEType=0;uint8_t nSrcDam=128;int nAuraState=-1;};
struct D2StatListStrc{};
struct Tables{int nStatesTxtRecordCount=64;} tables;auto* sgptDataTables=&tables;
D2UnitStrc target;D2SkillsTxt row;D2CombatStrc combat;D2StatListStrc list;
int hit=1,percent=50,flat=1,convert=0,multiplier=1,resist=0,fillCalls=0,totalCalls=0,elementCalls=0,range=0;
bool hasRow=true,hasTarget=true,town=false;int usedSrc=0;
#define D2_ALLOC_STRC_POOL(pool,type) (&combat)
void SUNITDMG_FillDamageValues(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,D2DamageStrc* p,int,uint8_t src){fillCalls++;usedSrc=src;p->dwPhysDamage=(100*256)*(100+p->dwEnDmgPct)/100*multiplier;}
void SUNITDMG_CalculateTotalDamage(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,D2DamageStrc* p){totalCalls++;p->dwPhysDamage=p->dwPhysDamage*(100-resist)/100;p->dwDmgTotal=p->dwPhysDamage;}
int STATLIST_UnitGetStatValue(D2UnitStrc* p,int,int){return p->hp;}
void STATLIST_SetUnitStat(D2UnitStrc* p,int,int value,int){p->hp=value;}
'''+alloc+r'''
D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int){return hasRow?&row:nullptr;}
D2UnitStrc* SUNIT_GetTargetUnit(D2GameStrc*,D2UnitStrc*){return hasTarget?&target:nullptr;}
void* UNITS_GetRoom(D2UnitStrc*){return nullptr;}
bool DUNGEON_IsRoomInTown(void*){return town;}
void sub_6FD15470(D2UnitStrc*,int value){range=value;}
int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int calc,int,int){return calc==0?percent:calc==1?flat:calc==2?0:convert;}
int SUNITDMG_GetResultFlags(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,int,int){return hit;}
int SKILLS_GetToHitFactor(D2UnitStrc*,int,int){return 20;}
void D2GAME_RollElementalDamage_6FD14DD0(D2UnitStrc*,D2DamageStrc*,int,int){elementCalls++;}
D2DamageStrc* SUNITDMG_GetDamageFromUnits(D2UnitStrc* p,D2UnitStrc*){return p->pCombat?&p->pCombat->tDamage:nullptr;}
D2StatListStrc* STATLIST_GetStatListFromUnitAndState(D2UnitStrc*,int){return &list;}
D2StatListStrc* STATLIST_AllocStatList(void*,int,int,int,int){return &list;}
void STATLIST_SetState(D2StatListStrc*,int){}
void D2COMMON_10475_PostStatToStatList(D2UnitStrc*,D2StatListStrc*,int){}
void sub_6FD10E50(){}
void STATLIST_SetStatRemoveCallback(D2StatListStrc*,void(*)()){}
void STATES_ToggleState(D2UnitStrc*,int,int){}
void sub_6FCFE0E0(D2UnitStrc*,D2StatListStrc*,D2SkillsTxt*,int,int){}
'''+start+'\nvoid CommitHealth(D2UnitStrc* pDefender,D2DamageStrc* pDamage){'+health+'}\n'+r'''
int main(){D2GameStrc game;bool first=true;std::printf("{\"cases\":[");
for(int h:{0,1})for(int f:{0,1,20})for(int m:{1,2})for(int r:{0,50,100})for(int src:{0,128}){
 hit=h;flat=f;multiplier=m;resist=r;row.nSrcDam=src;fillCalls=totalCalls=elementCalls=0;usedSrc=0;D2UnitStrc owner;
 int ok=SKILLS_SrvSt32_Conversion_Bash_Stun_Concentrate_BearSmite(&game,&owner,126,1);auto* d=SUNITDMG_GetDamageFromUnits(&owner,&target);target.hp=10000000;CommitHealth(&target,d);
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",h,f,m,r,src,ok,d->dwPhysDamage,fillCalls,totalCalls,elementCalls,usedSrc,d->dwDmgTotal,10000000-target.hp);
}std::printf("],\"guards\":[");
for(int guard:{0,1,2}){hasRow=guard!=0;hasTarget=guard!=1;town=guard==2;D2UnitStrc owner;if(guard)std::printf(",");std::printf("%d",SKILLS_SrvSt32_Conversion_Bash_Stun_Concentrate_BearSmite(&game,&owner,126,1));}std::printf("]}\n");}
'''
path=STAGE/'barbarian-attack-source-probe.cpp';path.write_text(cpp);binary=STAGE/'barbarian-attack-source-probe'
subprocess.run(['g++','-std=c++20','-O2',str(path),'-o',str(binary)],check=True)
data=json.loads(subprocess.check_output([str(binary)]));assert data['guards']==[0,0,0]
for hit,flat,multiplier,resist,src,ok,physical,fill,total,elements,used,stored,lost in data['cases']:
 assert (ok,physical,fill,total,elements,used)==(1,(150*256*multiplier*(100-resist)//100 if hit else 0)+flat*256,hit,hit,hit,128 if hit else 0)
 assert stored==lost==(150*256*multiplier*(100-resist)//100 if hit else 0)
(STAGE/'barbarian-attack-source-probe.json').write_text(json.dumps(data,indent=2)+'\n')
evidence={'referenceVersion':'3.3.93847','legacyCommit':tree['sha'],'sourceHashes':{p:hashlib.sha256((BASE/p).read_bytes()).hexdigest() for p in paths},
'probe':{'cases':len(data['cases']),'guards':3,'start32Sha256':hashlib.sha256(start.encode()).hexdigest(),'allocCombatSha256':hashlib.sha256(alloc.encode()).hexdigest(),'healthBlockSha256':hashlib.sha256(health.encode()).hexdigest(),'dataPath':'.local/class-skills/barbarian-attack-source-probe.json'},
'established':['Start32 prepares hit/stat/formulas/elements, calls allocation then adds calc2*256 to physical.','Unchanged allocation fills ordinary physical and calls CalculateTotalDamage only for successful/unblocked hits.','Flat addition occurs after ordinary critical and mitigation; it changes dwPhysDamage but not dwDmgTotal. The unchanged ExecuteEvents health block commits dwDmgTotal, so the added field alone does not increase legacy health loss.','Inspected shared skill-start handler consumes mana once after successful start when usemanaondo is absent; pinned Bash/Stun have no such flag.'],
'stubs':['Base physical100, source physical percentage, critical multiplier, resistance and stored total calculation are explicit bounded stubs; actual source implementations are not compiled here.','Element roll is counted only; aura-state mutation is stubbed and excluded by nAuraState=-1. Concentrate is not certified.','Combat allocation uses one explicit static record; no native memory/event/mode mechanics are certified.'],
'limits':['Legacy source isolation is not a modern D2R executable certificate, including Bash stale-total/physical-event behavior.','Control species/modes and stun duration are separately checked with shared control consumers.','Source event/item/curse/shield/PvP/owner/client timing remains bounded acceptance work.']}
(ROOT/'tools/reference/barbarian-attack-engine-evidence.json').write_text(json.dumps(evidence,indent=2)+'\n')
print(f"Attack start32/allocation: {len(data['cases'])} unchanged routine cases and three guards pass.")
