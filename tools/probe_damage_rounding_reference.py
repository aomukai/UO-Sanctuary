#!/usr/bin/env python3
"""Compile original resistance/absorb routine and life subtraction block with declared stubs."""
from pathlib import Path
import hashlib
import json
import subprocess

ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills'
path=ROOT/'.local/research/d2moo-SUnitDmg.cpp'
assert hashlib.sha256(path.read_bytes()).hexdigest()=='56ab5e14f7aceb18681f1c1e720e89c63bfe2a25e2e41b8aa9907652a7c9daf7'
text=path.read_text()
def block(start):
    brace=text.index('{',start);end=brace+1;depth=1
    while depth:
        depth+=(text[end]=='{')-(text[end]=='}');end+=1
    return text[start:end]
at=text.index('void __fastcall SUNITDMG_ApplyResistancesAndAbsorb(')
resistance=block(at)
at=text.index('if (pDamage->dwDmgTotal > 0)',text.index('void __fastcall SUNITDMG_ExecuteEvents('))
life=block(at)
cpp=r'''#include <algorithm>
#include <cstddef>
#include <cstdint>
#include <cstdio>
#define __fastcall
constexpr int STAT_DAMAGERESIST=1,STAT_MAGICRESIST=2,STAT_FIRE=3,STAT_MAXFIRE=4,STAT_ABSPCT=5,STAT_ABSFLAT=6,STAT_PIERCE=7,STAT_HITPOINTS=8;
constexpr int STATE_SANCTUARY=1,DIFFMODE_NIGHTMARE=1,DIFFMODE_HELL=2;
struct D2UnitStrc{int stats[9]={};};
struct D2DamageStrc{int value=0,dwAbsLife=0,dwDmgTotal=0;};
struct D2GameStrc{int bExpansion=0,nDifficulty=0;};
struct Difficulty{int dwResistPenalty=0;};
struct D2DamageInfoStrc{D2DamageStrc* pDamage;D2UnitStrc* pAttacker;D2UnitStrc* pDefender;D2GameStrc* pGame;Difficulty* pDifficultyLevelsTxt;int bDefenderIsMonster=1,nDamageReduction[1]={};};
struct D2DamageStatTableStrc{int nOffsetInDamageStrc=offsetof(D2DamageStrc,value),nResStatId=STAT_FIRE,nPierceStatId=STAT_PIERCE,nMaxResStatId=STAT_MAXFIRE,nDamageReductionType=0,nAbsorbPctStatId=STAT_ABSPCT,nAbsorbStatId=STAT_ABSFLAT;};
int STATLIST_UnitGetStatValue(D2UnitStrc* p,int stat,int){return p->stats[stat];}
void STATLIST_SetUnitStat(D2UnitStrc* p,int stat,int value,int){p->stats[stat]=value;}
bool STATES_CheckState(D2UnitStrc*,int){return false;}
bool MONSTERS_IsUndead(D2UnitStrc*){return false;}
int D2Clamp(int value,int minimum,int maximum){return std::clamp(value,minimum,maximum);}
int MONSTERUNIQUE_CalculatePercentage(int value,int percent,int denominator){return int(int64_t(value)*percent/denominator);}
'''
cpp+=resistance
cpp+='\nvoid ApplyLife(D2UnitStrc* pDefender,D2DamageStrc* pDamage){'+life+'}\n'
cpp+=r'''
int main(){bool first=true;std::printf("{\"reduction\":[");
for(int value:{0,1,209,235,512,2560})for(int resist:{-100,0,50,100,150})for(int reduction:{0,256})for(int pct:{0,37,100})for(int flat:{0,1})for(int monster:{0,1})for(int bypass:{0,1}){
 D2UnitStrc a,d;D2GameStrc game;Difficulty diff;D2DamageStrc damage;D2DamageStatTableStrc table;
 d.stats[STAT_FIRE]=resist;d.stats[STAT_ABSPCT]=pct;d.stats[STAT_ABSFLAT]=flat;damage.value=value;
 D2DamageInfoStrc info{&damage,&a,&d,&game,&diff,monster,{reduction}};
 SUNITDMG_ApplyResistancesAndAbsorb(&info,&table,bypass);
 if(!first)std::printf(",");first=false;
 std::printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d]",value,resist,reduction,pct,flat,monster,bypass,damage.value,damage.dwAbsLife);
}
std::printf("],\"life\":[");first=true;
for(int hp:{1,255,256,257,511,512,513,1024})for(int hit:{0,1,128,255,256,257,511,1024}){
 D2UnitStrc d;d.stats[STAT_HITPOINTS]=hp;D2DamageStrc damage;damage.dwDmgTotal=hit;ApplyLife(&d,&damage);
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d]",hp,hit,d.stats[STAT_HITPOINTS]);
}std::printf("]}\n");}
'''
source=STAGE/'damage-rounding-source-probe.cpp';source.write_text(cpp)
binary=STAGE/'damage-rounding-source-probe'
subprocess.run(['g++','-std=c++20','-O2',str(source),'-o',str(binary)],check=True)
out=subprocess.check_output([str(binary)]);data=json.loads(out)
for value,resist,reduction,pct,flat,monster,bypass,result,absorbed in data['reduction']:
    if value<=0:assert (result,absorbed)==(0,0);continue
    effective=resist if monster else min(resist,75)
    effective=max(effective,-100)
    if bypass:effective=min(effective,0)
    raw=value if bypass else value-reduction
    if raw>0 and effective:raw=raw*(100-min(effective,100))//100
    absorb=0
    if not bypass:
        product=raw*min(pct,40)
        absorb=(product//100 if product>=0 else -((-product)//100)) if pct>0 else 0
        if flat>0:absorb+=min(raw-absorb,flat*256)
    assert (result,absorbed)==(raw-absorb,absorb)
for hp,damage,after in data['life']:
    assert after==(hp if damage==0 else hp-damage if hp-damage>=256 else 0)
(STAGE/'damage-rounding-source-probe.json').write_bytes(out)
print(f'Damage rounding: {len(data["reduction"])} original reducer cases and {len(data["life"])} original life-subtraction cases pass; stat/room/difficulty/species stubs explicit.')
