#!/usr/bin/env python3
"""Compile immutable Sacrifice callbacks; explicit damage/formula/event stubs bound the proof."""
from pathlib import Path
import hashlib
import json
import subprocess

ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills'
SOURCE=STAGE/'SkillPal.cpp.reference'
assert hashlib.sha256(SOURCE.read_bytes()).hexdigest()=='c421857a633b5dd1f441eded1c5f41cbd88b375ba9dd771c3fa802e39591bdf0'
text=SOURCE.read_text()
def routine(name):
    at=text.index(name+'(');start=text.rfind('\n',0,at)+1;end=text.index('{',at)+1;depth=1
    while depth:
        depth+=(text[end]=='{')-(text[end]=='}');end+=1
    return text[start:end]
cpp=r'''#include <algorithm>
#include <cstdint>
#include <cstdio>
#include <string>
#define __fastcall
constexpr int DAMAGERESULTFLAG_SUCCESSFULHIT=1,STAT_HITPOINTS=0;
struct D2GameStrc{};
struct D2DamageStrc{int wResultFlags=0,dwPhysDamage=0,dwConvPct=0,nConvType=0,dwHitFlags=0,dwDmgTotal=0;};
struct D2SkillsTxt{int dwCalc[4]={0,1,2,3},nSrcDam=128,nEType=0,wResultFlags=0;};
struct D2UnitStrc{int hp=256,hit=1,hasSkill=1,hasTarget=1,inRange=1,hasDamage=1;};
D2SkillsTxt row;D2DamageStrc stored,selfDamage;D2UnitStrc target;
int hasRecord=1,prepared=1024,pct=8,bonusCalls=0,elementCalls=0,allocated=0,bonusPercent=0,src=0;
std::string events;
D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int){return hasRecord?&row:nullptr;}
void* UNITS_GetUsedSkill(D2UnitStrc* p){return p->hasSkill?p:nullptr;}
D2UnitStrc* SUNIT_GetTargetUnit(D2GameStrc*,D2UnitStrc* p){return p->hasTarget?&target:nullptr;}
int UNITS_IsInMeleeRange(D2UnitStrc* p,D2UnitStrc*,int){return p->inRange;}
int SUNITDMG_GetResultFlags(D2GameStrc*,D2UnitStrc* p,D2UnitStrc*,int,int){return p->hit;}
int SKILLS_GetToHitFactor(D2UnitStrc*,int,int){return 20;}
int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int calc,int,int){return calc==0?465:calc==1?pct:0;}
int SUNITDMG_ApplyDamageBonuses(D2UnitStrc*,int,void*,int,int,int bonus,int,int source){bonusCalls++;bonusPercent=bonus;src=source;return prepared;}
void D2GAME_RollElementalDamage_6FD14DD0(D2UnitStrc*,D2DamageStrc*,int,int){elementCalls++;}
void SUNITDMG_AllocCombat(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,D2DamageStrc* p,int source){allocated++;stored=*p;src=source;}
D2DamageStrc* SUNITDMG_GetDamageFromUnits(D2UnitStrc* p,D2UnitStrc*){return p->hasDamage?&stored:nullptr;}
int STATLIST_UnitGetStatValue(D2UnitStrc* p,int,int){return p->hp;}
int MONSTERUNIQUE_CalculatePercentage(int value,int numerator,int denominator){return int(int64_t(value)*numerator/denominator);}
void SUNITDMG_ExecuteEvents(D2GameStrc*,D2UnitStrc* a,D2UnitStrc* b,int,D2DamageStrc* p){events+=a==b?'E':'X';selfDamage=*p;}
void SUNITDMG_ExecuteMissileDamage(D2GameStrc*,D2UnitStrc* a,D2UnitStrc* b,D2DamageStrc*){events+=a==b?'M':'X';}
void SUNITDMG_DrainItemDurability(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,int){events+='D';}
'''
cpp+=routine('SKILLS_SrvSt29_Sacrifice')+'\n'+routine('SKILLS_SrvDo064_Sacrifice')
cpp+=r'''
int main(){bool first=true;std::printf("{\"costCases\":[");
for(int percent:{1,2,8,37})for(int physical:{0,1,255,256,257,1024,65535})for(int life:{0,1,255,256,257,1024,65535}){
 D2UnitStrc p;stored={};stored.dwPhysDamage=physical;target.hp=life;pct=percent;events.clear();selfDamage={};
 int ok=SKILLS_SrvDo064_Sacrifice(nullptr,&p,96,20);if(!first)std::printf(",");first=false;
 std::printf("[%d,%d,%d,%d,%d,%d,%d,\"%s\"]",percent,physical,life,ok,selfDamage.dwPhysDamage,selfDamage.dwDmgTotal,selfDamage.dwHitFlags,events.c_str());
}std::printf("],\"startCases\":[");first=true;
for(int guard:{0,1,2,3,4,5}){
 D2UnitStrc p;hasRecord=1;stored={};allocated=bonusCalls=elementCalls=bonusPercent=src=0;
 if(guard==1)hasRecord=0;if(guard==2)p.hasSkill=0;if(guard==3)p.hasTarget=0;if(guard==4)p.inRange=0;if(guard==5)p.hit=0;
 int ok=SKILLS_SrvSt29_Sacrifice(nullptr,&p,96,20);if(!first)std::printf(",");first=false;
 std::printf("[%d,%d,%d,%d,%d,%d,%d,%d]",guard,ok,allocated,bonusCalls,elementCalls,bonusPercent,src,stored.dwPhysDamage);
}std::printf("],\"doGuards\":[");
for(int guard:{0,1,2}){D2UnitStrc p;hasRecord=1;events.clear();if(guard==0)hasRecord=0;if(guard==1)p.hasTarget=0;if(guard==2)p.hasDamage=0;
 if(guard)std::printf(",");std::printf("[%d,\"%s\"]",SKILLS_SrvDo064_Sacrifice(nullptr,&p,96,20),events.c_str());}
std::printf("]}\n");}
'''
path=STAGE/'sacrifice-source-probe.cpp';path.write_text(cpp)
binary=STAGE/'sacrifice-source-probe'
subprocess.run(['g++','-std=c++20','-O2',str(path),'-o',str(binary)],check=True)
out=subprocess.check_output([str(binary)]);data=json.loads(out)
for percent,physical,life,ok,self_physical,total,flags,events in data['costCases']:
    assert ok==1 and self_physical==total==min(physical,life)*percent//100 and flags==4096 and events=='EMD'
assert data['startCases']==[[0,1,1,1,1,465,128,1024],*[ [guard,0,0,0,0,0,0,0] for guard in range(1,5) ],[5,1,1,0,0,0,128,0]]
assert data['doGuards']==[[0,''],[0,''],[1,'']]
(STAGE/'sacrifice-source-probe.json').write_bytes(out)
print(f"Sacrifice: {len(data['costCases'])} original cost/cap/order cases, six start cases and three do guards pass. Damage construction, death and item events are explicit stubs.")
