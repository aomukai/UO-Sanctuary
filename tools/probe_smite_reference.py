#!/usr/bin/env python3
"""Compile original Smite, physical endpoints, stun block and attack-rate/end-cycle routines with declared stubs."""
from pathlib import Path
import hashlib,json,subprocess
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'
pal=STAGE/'SkillPal.cpp.reference';damage=ROOT/'.local/research/d2moo-SUnitDmg.cpp';units=ROOT/'.local/research/d2moo-CommonUnits.cpp'
assert hashlib.sha256(pal.read_bytes()).hexdigest()=='c421857a633b5dd1f441eded1c5f41cbd88b375ba9dd771c3fa802e39591bdf0'
assert hashlib.sha256(damage.read_bytes()).hexdigest()=='56ab5e14f7aceb18681f1c1e720e89c63bfe2a25e2e41b8aa9907652a7c9daf7'
assert hashlib.sha256(units.read_bytes()).hexdigest()=='a226a3d1f3fbe062a1ab6d8807c54058dcd6bc88bd3604b6fcdfad1d229554ea'
def routine(text,name):
 at=text.index(name+'(');start=text.rfind('\n',0,at)+1;end=text.index('{',at)+1;depth=1
 while depth:depth+=(text[end]=='{')-(text[end]=='}');end+=1
 return text[start:end]
cpp=r'''#include <algorithm>
#include <cstdint>
#include <cstdio>
#include <cstddef>
#include <string>
#define __fastcall
#define __stdcall
#define __forceinline
#define D2_ASSERT(x)
constexpr int UNIT_PLAYER=0,UNIT_MONSTER=1,MONMODE_ATTACK2=5,STATE_HOLYSHIELD=1,STATE_STUNNED=2,EVENTTYPE_REMOVESTATE=0,PLRMODE_SEQUENCE=18,STATEMASK_MELEEONLY=0,BODYLOC_RARM=0,BODYLOC_LARM=1,ITEMTYPE_WEAPON=0;
constexpr int DAMAGERESULTFLAG_SUCCESSFULHIT=1,STAT_SECONDARY_MINDAMAGE=0,STAT_SECONDARY_MAXDAMAGE=1,STAT_MINDAMAGE=2,STAT_MAXDAMAGE=3,STAT_ITEM_NORMALDAMAGE=4,STAT_DAMAGEPERCENT=5,STAT_STRENGTH=6,STAT_DEXTERITY=7,STAT_ITEM_MINDAMAGE_PERCENT=8,STAT_ITEM_MAXDAMAGE_PERCENT=9,STAT_ITEM_FASTERATTACKRATE=10,STAT_ATTACKRATE=11;
template<class T>T D2Clamp(T v,T lo,T hi){return std::clamp(v,lo,hi);}
struct D2GameStrc{int dwGameFrame=100;void* pMemoryPool=nullptr;};
struct Anim{int dwAnimSpeed=256;};
struct D2UnitStrc{int dwUnitType=0,dwClassId=0,dwUnitId=1,pSeed=0,strength=25,percent=0,normal=0,hasTarget=1,inRange=1,hasShield=1,holy=0,hit=0,boss=0,unique=0,hireling=0,velocity=3,ias=0,attackRate=100,dwAnimMode=0,dwSeqSpeed=0,wAnimSpeed=0,nSeqCurrentFramePrecise=0,dwFrameCountPrecise=12*256;void* pInventory=this;void* pAnimSeq=nullptr;D2GameStrc* pGame=nullptr;Anim* pAnimData=nullptr;};
struct D2DamageStrc{int dwPhysDamage=0,dwStunLen=0,wResultFlags=0,dwHitFlags=0,dwHitClass=0,dwConvPct=0,nConvType=0;};
struct D2SkillsTxt{int dwCalc[4]={0,1,2,3},nEType=0,wResultFlags=8,dwHitFlags=0;};
struct D2ItemsTxt{int nMinDam=1,nMaxDam=3;};
struct D2StatListStrc{int expiry=0;};struct MonStats{int nVelocity=3;};
D2SkillsTxt skill;D2ItemsTxt shieldRow;D2UnitStrc shield,target;D2DamageStrc stored;D2StatListStrc state;MonStats monster;
int hasSkill=1,hasItemRow=1,skillDamage=15,stun=15,holyMin=768,holyMax=1536,elementCalls=0,allocationCalls=0,drainCalls=0,randomRoll=0;bool existingState=false;
D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int){return hasSkill?&skill:nullptr;}
D2UnitStrc* SUNIT_GetTargetUnit(D2GameStrc*,D2UnitStrc* p){return p && p->hasTarget?&target:nullptr;}
int UNITS_IsInMeleeRange(D2UnitStrc* p,D2UnitStrc*,int){return p->inRange;}
void sub_6FC627B0(D2UnitStrc*,int){}
int INVENTORY_GetEquippedShield(void* inventory,D2UnitStrc** item){auto p=(D2UnitStrc*)inventory;*item=&shield;return p->hasShield;}
D2ItemsTxt* DATATBLS_GetItemsTxtRecord(int){return hasItemRow?&shieldRow:nullptr;}
int STATES_CheckState(D2UnitStrc* p,int){return p->holy;}
D2StatListStrc* STATLIST_GetStatListFromUnitAndState(D2UnitStrc*,int){return existingState?&state:nullptr;}
int STATLIST_GetSkillId(D2StatListStrc*){return 117;}
int STATLIST_GetSkillLevel(D2StatListStrc*){return 1;}
int SKILLS_GetMinPhysDamage(D2UnitStrc*,int,int,int){return holyMin;}
int SKILLS_GetMaxPhysDamage(D2UnitStrc*,int,int,int){return holyMax;}
int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int calc,int,int){return calc==0?skillDamage:calc==1?stun:0;}
void D2GAME_RollElementalDamage_6FD14DD0(D2UnitStrc*,D2DamageStrc*,int,int){elementCalls++;}
int SUNITDMG_GetResultFlags(D2GameStrc*,D2UnitStrc* p,D2UnitStrc*,int,int){return p->hit;}
void SUNITDMG_AllocCombat(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,D2DamageStrc* p,int){stored=*p;allocationCalls++;}
void SUNITDMG_DrainItemDurability(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,int){drainCalls++;}
D2UnitStrc* sub_6FC7C7B0(D2UnitStrc*){return nullptr;}
int INVENTORY_GetWieldType(D2UnitStrc*,void*){return 0;}
int STATLIST_UnitGetStatValue(D2UnitStrc* p,int stat,int){return stat==STAT_STRENGTH?p->strength:stat==STAT_DAMAGEPERCENT?p->percent:stat==STAT_ATTACKRATE?p->attackRate:0;}
int STATLIST_UnitGetItemStatOrSkillStatValue(D2UnitStrc* p,int stat,int){return stat==STAT_ITEM_NORMALDAMAGE?p->normal:stat==STAT_ITEM_FASTERATTACKRATE?p->ias:0;}
int ITEMS_GetStrengthBonus(D2UnitStrc*){return 100;}int ITEMS_GetDexBonus(D2UnitStrc*){return 0;}
int SKILLS_GetWeaponMasteryBonus(D2UnitStrc*,D2UnitStrc*,int,int){return 0;}
int MONSTERUNIQUE_CalculatePercentage(int a,int b,int d){return int(int64_t(a)*b/d);}
int ITEMS_RollLimitedRandomNumber(int* seed,int limit){return std::clamp(*seed,0,limit-1);}
int MONSTERUNIQUE_CheckMonTypeFlag(D2UnitStrc* p,int){return p->unique;}
int ITEMS_RollRandomNumber(int*){return randomRoll;}
int MONSTERS_IsBoss(void*,D2UnitStrc* p){return p->boss;}
MonStats* MONSTERMODE_GetMonStatsTxtRecord(int){return &monster;}
int MONSTERS_GetHirelingTypeId(D2UnitStrc* p){return p->hireling;}
void STATLIST_SetExpireFrame(D2StatListStrc* p,int n){p->expiry=n;}
void EVENT_SetEvent(D2GameStrc*,D2UnitStrc*,int,int,int,int){}
D2StatListStrc* STATLIST_AllocStatList(void*,int,int expiry,int,int){state.expiry=expiry;return &state;}
void STATLIST_SetState(D2StatListStrc*,int){}
void sub_6FD10E50(){}
void STATLIST_SetStatRemoveCallback(D2StatListStrc*,void(*)()){}
void D2COMMON_10475_PostStatToStatList(D2UnitStrc*,D2StatListStrc*,int){}
void STATES_ToggleState(D2UnitStrc*,int,int){}
void UNITS_MergeDualWieldWeaponStatLists(D2UnitStrc*,int){}int UNITS_CanDualWield(D2UnitStrc*){return 0;}
D2UnitStrc* INVENTORY_GetItemFromBodyLoc(void*,int){return nullptr;}int ITEMS_CanBeEquipped(D2UnitStrc*){return 0;}int ITEMS_CheckItemTypeId(D2UnitStrc*,int){return 0;}
int STATES_CheckStateMaskOnUnit(D2UnitStrc*,int){return 0;}int D2Common_11043(D2UnitStrc*){return 0;}
'''
cpp+=routine(damage.read_text(),'SUNITDMG_ApplyDamageBonuses')+'\n'+routine(pal.read_text(),'SKILLS_SrvDo150_Smite')
t=damage.read_text();a=t.index('int32_t bApplyStun = 0;',t.index('void __fastcall SUNITDMG_ExecuteEvents'));b=t.index('\n\tSUNITDMG_ApplyColdState',a)
cpp+='\nint stunDecision(D2UnitStrc* pAttacker,D2UnitStrc* pDefender,int nStunLength){state={};\n'+t[a:b]+'\nreturn bApplyStun?state.expiry-pAttacker->pGame->dwGameFrame:0;}\n'
cpp+=routine(units.read_text(),'UNITS_UpdateAttackAnimRateAndVelocity')+'\n'+routine(units.read_text(),'UNITS_IsAtEndOfFrameCycle')
cpp+=r'''
int main(){bool first=true;D2GameStrc game;Anim anim;std::printf("{\"damageCases\":[");
for(int holy:{0,1})for(int strength:{25,100})for(int skillPct:{15,300})for(int normal:{0,40})for(int roll:{0,31})for(int flags:{0,16}){
 D2UnitStrc p;p.pGame=&game;p.pSeed=roll;p.strength=strength;p.normal=normal;p.holy=holy;p.hit=flags;existingState=holy;skillDamage=skillPct;
 stored={};elementCalls=allocationCalls=drainCalls=0;int ok=SKILLS_SrvDo150_Smite(&game,&p,97,1);
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",holy,strength,skillPct,normal,roll,flags,ok,stored.dwPhysDamage,stored.dwHitFlags,stored.wResultFlags,stored.dwHitClass,elementCalls,allocationCalls,drainCalls);
}std::printf("],\"guards\":[");for(int guard:{0,1,2,3,4}){D2UnitStrc p;hasSkill=hasItemRow=1;if(guard==0)hasSkill=0;if(guard==1)p.hasTarget=0;if(guard==2)p.inRange=0;if(guard==3)p.hasShield=0;if(guard==4)hasItemRow=0;if(guard)std::printf(",");std::printf("%d",SKILLS_SrvDo150_Smite(&game,&p,97,1));}
std::printf("],\"stunCases\":[");first=true;
for(int monsterType:{0,1})for(int unique:{0,1})for(int boss:{0,1})for(int velocity:{0,3})for(int hireling:{0,1})for(int roll:{89,90})for(int duration:{0,15,110,300}){
 D2UnitStrc a,d;a.pGame=&game;a.pSeed=roll;d.dwUnitType=monsterType;d.unique=unique;d.boss=boss;d.hireling=hireling;monster.nVelocity=velocity;randomRoll=roll;existingState=false;
 int result=stunDecision(&a,&d,duration);if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d,%d]",monsterType,unique,boss,velocity,hireling,roll,duration,result);
}std::printf("],\"timingCases\":[");first=true;
for(int ias:{0,10,30,50,100,200})for(int wsm:{-30,0,20})for(int skillRate:{0,35})for(int chill:{0,50}){
 D2UnitStrc p;p.pAnimData=&anim;p.ias=ias;p.attackRate=100-wsm+skillRate-chill;UNITS_UpdateAttackAnimRateAndVelocity(&p);int frames=0;
 while(!UNITS_IsAtEndOfFrameCycle(&p)){p.nSeqCurrentFramePrecise+=p.wAnimSpeed;frames++;}
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d]",ias,wsm,skillRate,chill,p.wAnimSpeed,frames);
}std::printf("]}\n");}
'''
p=STAGE/'smite-source-probe.cpp';p.write_text(cpp);binary=STAGE/'smite-source-probe';subprocess.run(['g++','-std=c++20','-O2',str(p),'-o',str(binary)],check=True)
out=subprocess.check_output([str(binary)]);d=json.loads(out)
for holy,strength,skill,normal,roll,flags,ok,physical,hits,result,hitclass,elements,alloc,drain in d['damageCases']:
 lo=(1+normal)*256+holy*768;hi=(3+normal)*256+holy*1536;lo+=lo*(skill+strength)//100;hi+=hi*(skill+strength)//100
 assert (ok,physical,hits,result,hitclass,elements,alloc,drain)==(1,lo+min(roll,hi-lo-1),2,flags|9,101,1,1,1)
assert d['guards']==[0]*5
for kind,unique,boss,velocity,hireling,roll,duration,result in d['stunCases']:
 expected=0
 if duration>0:
  if kind!=1:expected=min(duration,250)
  if (not unique or roll>=90) and not boss and velocity:expected=min(duration,13 if hireling else 250)
 assert result==expected,(kind,unique,boss,velocity,hireling,roll,duration,result,expected)
for ias,wsm,skill,chill,speed,frames in d['timingCases']:
 advance=256*max(15,min(175,100+120*ias//(120+ias)-wsm+skill-chill))//100
 assert speed==advance and frames==(12*256+advance-1)//advance-1
(STAGE/'smite-source-probe.json').write_bytes(out)
print({k:len(v) for k,v in d.items()})
