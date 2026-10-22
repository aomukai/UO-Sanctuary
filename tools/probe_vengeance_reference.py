#!/usr/bin/env python3
"""Run immutable Vengeance start routine with explicit stat/formula/combat stubs.

This isolates the original elemental base roll, mastery rounding, miss behavior
and hit-class cycling. It does not certify modern hit resolution or item events.
"""
from pathlib import Path
import hashlib
import json
import subprocess

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills'
PATH = ROOT / '.local/class-skills/SkillPal.cpp.reference'
assert hashlib.sha256(PATH.read_bytes()).hexdigest() == 'c421857a633b5dd1f441eded1c5f41cbd88b375ba9dd771c3fa802e39591bdf0'
text = PATH.read_text()
at = text.index('SKILLS_SrvSt35_Vengeance(')
start = text.rfind('\n', 0, at) + 1
brace = text.index('{', at)
end = brace + 1
depth = 1
while depth:
    depth += (text[end] == '{') - (text[end] == '}')
    end += 1

cpp = r'''#include <algorithm>
#include <cstdint>
#include <cstdio>
#define __fastcall
constexpr int DAMAGERESULTFLAG_SUCCESSFULHIT=1;
constexpr int STAT_SECONDARY_MINDAMAGE=1,STAT_SECONDARY_MAXDAMAGE=2,STAT_MINDAMAGE=3,STAT_MAXDAMAGE=4;
constexpr int STAT_PASSIVE_FIRE_MASTERY=5,STAT_PASSIVE_COLD_MASTERY=6,STAT_PASSIVE_LTNG_MASTERY=7;
struct D2SkillStrc{int param=0;};
struct D2DamageStrc{int wResultFlags=0,dwHitFlags=0,dwPhysDamage=0,dwFireDamage=0,dwColdDamage=0,dwLtngDamage=0,dwColdLen=0,dwHitClass=0;};
struct D2SkillsTxt{int nSrcDam=128,wResultFlags=0,dwHitFlags=0,dwCalc[3]={0,1,2};};
struct D2GameStrc{};
struct D2UnitStrc{
 void* pInventory=this;int pSeed=0,hit=1,hasSkill=1,hasTarget=1;
 int alternate=0,wield=0,minimum=10,maximum=20,secondMin=50,secondMax=60;
 int mastery[3]={},percent[3]={70,80,90},coldFrames=30;D2SkillStrc skill;
};
D2DamageStrc allocated;
D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int){static D2SkillsTxt row;return &row;}
D2SkillStrc* UNITS_GetUsedSkill(D2UnitStrc* p){return p->hasSkill?&p->skill:nullptr;}
D2UnitStrc* SUNIT_GetTargetUnit(D2GameStrc*,D2UnitStrc* p){return p->hasTarget?p:nullptr;}
int SUNITDMG_GetResultFlags(D2GameStrc*,D2UnitStrc* p,D2UnitStrc*,int,int){return p->hit;}
int SKILLS_GetToHitFactor(D2UnitStrc*,int,int){return 20;}
int SUNITDMG_ApplyDamageBonuses(D2UnitStrc*,int,void*,int,int,int,int,int){return 12800;}
bool sub_6FC7C7B0(D2UnitStrc* p){return p->alternate;}
int INVENTORY_GetWieldType(D2UnitStrc* p,void*){return p->wield;}
int STATLIST_UnitGetStatValue(D2UnitStrc* p,int stat,int){switch(stat){
case STAT_MINDAMAGE:return p->minimum;case STAT_MAXDAMAGE:return p->maximum;
case STAT_SECONDARY_MINDAMAGE:return p->secondMin;case STAT_SECONDARY_MAXDAMAGE:return p->secondMax;
default:return p->mastery[stat-STAT_PASSIVE_FIRE_MASTERY];}}
int ITEMS_RollLimitedRandomNumber(int* seed,int limit){return std::clamp(*seed,0,limit-1);}
int SKILLS_EvaluateSkillFormula(D2UnitStrc* p,int calc,int,int){return p->percent[calc];}
int MONSTERUNIQUE_CalculatePercentage(int value,int numerator,int denominator){return int(int64_t(value)*numerator/denominator);}
int SKILLS_GetElementalLength(D2UnitStrc* p,int,int,int){return p->coldFrames;}
int SKILLS_GetParam1(D2SkillStrc* p){return p->param;}
void SKILLS_SetParam1(D2SkillStrc* p,int value){p->param=value;}
void SUNITDMG_AllocCombat(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,D2DamageStrc* p,int){allocated=*p;}
'''
cpp += text[start:end]
cpp += r'''
int main(){bool first=true;std::printf("{\"elementCases\":[");
for(int hit:{0,1})for(int alternate:{0,1})for(int mastery:{0,37,100})for(int roll:{0,2559})for(int phase:{0,1,2}){
 D2UnitStrc p;p.hit=hit;p.alternate=alternate;p.wield=2;p.pSeed=roll;p.skill.param=phase;
 p.mastery[0]=mastery;p.mastery[1]=mastery;p.mastery[2]=mastery;
 allocated={};int ok=SKILLS_SrvSt35_Vengeance(nullptr,&p,111,1);
 if(!first)std::printf(",");first=false;
 std::printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",hit,alternate,mastery,roll,phase,ok,allocated.dwPhysDamage,allocated.dwFireDamage,allocated.dwColdDamage,allocated.dwLtngDamage,allocated.dwColdLen,allocated.dwHitClass,p.skill.param);
}std::printf("],\"guards\":[");
for(int guard:{0,1}){D2UnitStrc p;if(guard)p.hasTarget=0;else p.hasSkill=0;if(guard)std::printf(",");std::printf("%d",SKILLS_SrvSt35_Vengeance(nullptr,&p,111,1));}
std::printf("]}\n");}
'''
path = STAGE / 'vengeance-source-probe.cpp'
path.write_text(cpp)
binary = STAGE / 'vengeance-source-probe'
subprocess.run(['g++', '-std=c++20', '-O2', str(path), '-o', str(binary)], check=True)
out = subprocess.check_output([str(binary)])
data = json.loads(out)
assert data['guards'] == [0, 0]
for hit, alternate, mastery, roll, phase, ok, physical, fire, cold, lightning, length, hitclass, nextphase in data['elementCases']:
    assert ok == 1
    if not hit:
        assert (physical, fire, cold, lightning, length, hitclass, nextphase) == (0, 0, 0, 0, 0, 0, phase)
        continue
    basis = (50 if alternate else 10) * 256 + roll
    expected = [basis * (percent + percent * mastery // 100) // 100 for percent in [70, 80, 90]]
    assert [fire, cold, lightning] == expected
    assert (physical, length, hitclass, nextphase) == (12800, 30, [32, 48, 64][phase], (phase + 1) % 3)
(STAGE / 'vengeance-source-probe.json').write_bytes(out)
print(f'Vengeance: {len(data["elementCases"])} unchanged start-routine cases and two guards pass; stat/formula/RNG/allocation stubs explicit.')
