#!/usr/bin/env python3
"""Compile original weapon physical endpoint scaling/roll; stat/item/mastery/RNG stubs are explicit."""
from pathlib import Path
import hashlib,json,subprocess
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills';source=ROOT/'.local/research/d2moo-SUnitDmg.cpp'
assert hashlib.sha256(source.read_bytes()).hexdigest()=='56ab5e14f7aceb18681f1c1e720e89c63bfe2a25e2e41b8aa9907652a7c9daf7'
t=source.read_text();at=t.index('SUNITDMG_ApplyDamageBonuses(');start=t.rfind('\n',0,at)+1;end=t.index('{',at)+1;depth=1
while depth:depth+=(t[end]=='{')-(t[end]=='}');end+=1
cpp=r'''#include <algorithm>
#include <cstdint>
#include <cstdio>
#define __fastcall
constexpr int STAT_SECONDARY_MINDAMAGE=0,STAT_SECONDARY_MAXDAMAGE=1,STAT_MINDAMAGE=2,STAT_MAXDAMAGE=3,STAT_ITEM_NORMALDAMAGE=4,STAT_DAMAGEPERCENT=5,STAT_STRENGTH=6,STAT_DEXTERITY=7,STAT_ITEM_MINDAMAGE_PERCENT=8,STAT_ITEM_MAXDAMAGE_PERCENT=9;
struct D2UnitStrc{void* pInventory=this;int pSeed=0,minimum=1,maximum=2,strength=25,dexterity=26,percent=0,armed=0;};
D2UnitStrc* sub_6FC7C7B0(D2UnitStrc* p){return p->armed?p:nullptr;}
int INVENTORY_GetWieldType(D2UnitStrc*,void*){return 0;}
int STATLIST_UnitGetStatValue(D2UnitStrc* p,int stat,int){switch(stat){case STAT_MINDAMAGE:case STAT_SECONDARY_MINDAMAGE:return p->minimum;case STAT_MAXDAMAGE:case STAT_SECONDARY_MAXDAMAGE:return p->maximum;case STAT_STRENGTH:return p->strength;case STAT_DEXTERITY:return p->dexterity;case STAT_DAMAGEPERCENT:return p->percent;default:return 0;}}
int STATLIST_UnitGetItemStatOrSkillStatValue(D2UnitStrc*,int,int){return 0;}
int ITEMS_GetStrengthBonus(D2UnitStrc*){return 75;}
int ITEMS_GetDexBonus(D2UnitStrc*){return 75;}
int SKILLS_GetWeaponMasteryBonus(D2UnitStrc*,D2UnitStrc*,int,int){return 0;}
int MONSTERUNIQUE_CalculatePercentage(int value,int n,int d){return int(int64_t(value)*n/d);}
int ITEMS_RollLimitedRandomNumber(int* seed,int limit){return std::clamp(*seed,0,limit-1);}
'''+t[start:end]+r'''
int main(){bool first=true;std::printf("[");
for(int armed:{0,1})for(int minimum:{1,10,23})for(int strength:{1,25,100})for(int bonus:{-250,0,180,465,865})for(int percent:{0,250})for(int roll:{0,1,2047}){
 D2UnitStrc p;p.minimum=minimum;p.maximum=minimum+7;p.strength=strength;p.dexterity=strength+1;p.armed=armed;p.percent=percent;p.pSeed=roll;
 int result=SUNITDMG_ApplyDamageBonuses(&p,1,nullptr,0,0,bonus,0,128);
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d]",minimum,p.maximum,strength,bonus,percent,roll,result,armed,p.dexterity);
}std::printf("]\n");}
'''
p=STAGE/'weapon-physical-source-probe.cpp';p.write_text(cpp);binary=STAGE/'weapon-physical-source-probe';subprocess.run(['g++','-std=c++20','-O2',str(p),'-o',str(binary)],check=True)
out=subprocess.check_output([str(binary)]);data=json.loads(out)
def trunc(n,d):return abs(n)//d*(1 if n>=0 else -1)
for minimum,maximum,strength,skill,percent,roll,result,armed,dexterity in data:
    stat=strength*75//100+dexterity*75//100 if armed else strength
    bonus=max(-90,stat+skill+percent);lo=minimum*256;hi=maximum*256;lo+=trunc(lo*bonus,100);hi+=trunc(hi*bonus,100)
    assert result==lo+min(roll,hi-lo-1)
(STAGE/'weapon-physical-source-probe.json').write_bytes(out)
print(f'Physical weapon damage: {len(data)} original endpoint/roll cases pass; armed mixed stat/unarmed/mastery/RNG boundary declared, no critical/event proof.')
