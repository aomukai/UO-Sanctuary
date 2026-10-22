#!/usr/bin/env python3
"""Compile unchanged pinned legacy Thorns/item-return routines with explicit dispatch/mitigation stubs."""
from pathlib import Path
import hashlib,json,subprocess
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills'
SOURCES={'.local/research/d2moo-SUnitDmg.cpp':'56ab5e14f7aceb18681f1c1e720e89c63bfe2a25e2e41b8aa9907652a7c9daf7','.local/class-skills/SkillPal.cpp.reference':'c421857a633b5dd1f441eded1c5f41cbd88b375ba9dd771c3fa802e39591bdf0',
         '.local/class-skills/ThornsSkillItem.cpp.reference':'1045184b945928fe9f55a2806510dca02772b6867a6fd09bc7c685dc56df8696'}
def function(text,name):
    index=text.index(name+'(');start=text.rfind('\n',0,index)+1;brace=text.index('{',index);end=brace+1;depth=1
    while depth:
        depth+=(text[end]=='{')-(text[end]=='}');end+=1
    return text[start:end]
texts={}
for source,digest in SOURCES.items():
    data=(ROOT/source).read_bytes()
    if hashlib.sha256(data).hexdigest()!=digest:raise SystemExit('Pinned source cache missing/changed: '+source)
    texts[source]=data.decode()
source=r'''#include <cstdint>
#include <cstdio>
#include <vector>
#define __fastcall
constexpr int UNIT_PLAYER=0,UNIT_MONSTER=1,UNITFLAG_CANBEATTACKED=1,STAT_THORNS_PERCENT=1,STAT_HITPOINTS=2;
struct D2GameStrc{};
struct D2UnitStrc{int dwUnitType=1,dwFlags=1,percent=250,flat=4,hire=0,life256=256000;};
struct D2DamageStrc{int dwPhysDamage=0,dwHitClass=0;uint16_t wResultFlags=0;};
std::vector<D2DamageStrc> packets;
int STATLIST_UnitGetStatValue(D2UnitStrc* p,int stat,int){return stat==STAT_HITPOINTS?p->life256:p->percent;}
int STATLIST_UnitGetItemStatOrSkillStatValue(D2UnitStrc* p,int,int){return p->flat;}
int MONSTERS_GetHirelingTypeId(D2UnitStrc* p){return p->hire;}
int MONSTERUNIQUE_CalculatePercentage(int damage,int percent,int divisor){return (int)((int64_t)damage*percent/divisor);}
void SUNITDMG_ExecuteEvents(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,int,D2DamageStrc*){}
void SUNITDMG_ExecuteMissileDamage(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,D2DamageStrc* p){packets.push_back(*p);}
'''
source+=function(texts['.local/class-skills/SkillPal.cpp.reference'],'SKILLS_ApplyThornsDamage')+'\n'
source+=function(texts['.local/class-skills/ThornsSkillItem.cpp.reference'],'SKILLITEM_EventFunc06_AttackerTakesDamage')+'\n'
cap=texts['.local/research/d2moo-SUnitDmg.cpp']
start=cap.index('\tconst int32_t nHp = STATLIST_UnitGetStatValue(pDefender, STAT_HITPOINTS, 0);',cap.index('void __fastcall SUNITDMG_ExecuteEvents('))
end=cap.index('\n',cap.index('pDamage->dwPhysDamage = std::min(pDamage->dwPhysDamage, nHp);',start))
source+='\nvoid OriginalEventTailLifeCap(D2UnitStrc* pDefender,D2DamageStrc* pDamage){\n'+cap[start:end]+'\n}\n'
source+=r'''
int main(){D2GameStrc game;std::printf("{\"cases\":[");bool first=true;
for(int percent:{250,1010})for(int kind=0;kind<3;kind++)for(int incoming:{0,128,2560,25600})for(int life:{2560,256000}){
 D2UnitStrc defender,attacker;defender.percent=percent;defender.life256=life;attacker.dwUnitType=kind==1?UNIT_PLAYER:UNIT_MONSTER;attacker.hire=kind==2;
 D2DamageStrc damage;damage.dwPhysDamage=incoming;packets.clear();OriginalEventTailLifeCap(&defender,&damage);
 SKILLITEM_EventFunc06_AttackerTakesDamage(&game,0,&defender,&attacker,&damage,78<<16,0);
 SKILLS_ApplyThornsDamage(&game,&attacker,&defender,&damage);
 if(!first)std::printf(",");first=false;
 std::printf("{\"percent\":%d,\"attackerKind\":%d,\"prepared256\":%d,\"life256\":%d,\"eligible256\":%d,\"packets256\":[",percent,kind,incoming,life,damage.dwPhysDamage);
 for(unsigned i=0;i<packets.size();i++){if(i)std::printf(",");std::printf("%d",packets[i].dwPhysDamage);}
 std::printf("],\"flags\":%u}",packets[0].wResultFlags);
}std::printf("]}\n");}
'''
(STAGE/'thorns-source-probe.cpp').write_text(source)
subprocess.run(['g++','-std=c++20','-O2',str(STAGE/'thorns-source-probe.cpp'),'-o',str(STAGE/'thorns-source-probe')],check=True)
output=subprocess.check_output([str(STAGE/'thorns-source-probe')]);(STAGE/'thorns-source-probe.json').write_bytes(output)
data=json.loads(output)
for case in data['cases']:
    pct=(case['percent']+4)//8 if case['attackerKind'] else case['percent']
    assert case['packets256']==[1024,min(case['prepared256'],case['life256'])*pct//100],case
    assert case['flags']==0x4021
print('48 unchanged-source cases with extracted event-tail life cap passed: separate flat/percentage packets, fractional prepared damage, player/hireling rounding, soft-hit flags.')
