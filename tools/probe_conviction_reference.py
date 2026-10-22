#!/usr/bin/env python3
"""Probe unchanged pinned immunity reducer and defense getter with explicit stat/Holy Shield stubs."""
from pathlib import Path
import hashlib,json,subprocess
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'
SOURCES={'.local/class-skills/SkillNec.cpp.reference': '07259f95772794dc8e64cbd932c0cfe2950eb02ea52b4cccb9028d2cd36ae97d', '.local/research/d2moo-CommonUnits.cpp': 'a226a3d1f3fbe062a1ab6d8807c54058dcd6bc88bd3604b6fcdfad1d229554ea'}
for path,digest in SOURCES.items():
    if hashlib.sha256((ROOT/path).read_bytes()).hexdigest()!=digest:raise SystemExit("Pinned source cache missing/changed: "+path)
def function(text,name):
    at=text.index(name+'(');start=text.rfind('\n',0,at)+1;brace=text.index('{',at);end=brace+1;depth=1
    while depth:
        depth+=(text[end]=='{')-(text[end]=='}');end+=1
    return text[start:end]
source=r'''#include <algorithm>
#include <cstdint>
#include <cstdio>
#define __fastcall
#define __stdcall
constexpr int UNIT_PLAYER=0,UNIT_MONSTER=1,STAT_DAMAGERESIST=0,STAT_MAGICRESIST=1,STAT_FIRERESIST=2,STAT_LIGHTRESIST=3,STAT_COLDRESIST=4,STAT_POISONRESIST=5;
constexpr int STAT_DEXTERITY=6,STAT_ARMORCLASS=7,STAT_ITEM_ARMOR_PERCENT=8,STAT_SKILL_ARMOR_PERCENT=9,STATE_HOLYSHIELD=10,STAT_MODIFIERLIST_SKILL=11,STAT_MODIFIERLIST_LEVEL=12,STAT_ARMOR_OVERRIDE_PERCENT=13;
struct D2UnitStrc{int dwUnitType=1,hire=0,resist=0,dex=0,defense=1,percent=0;void* pInventory=nullptr;};
struct D2SkillsTxt{int dwCalc[1];};struct D2StatListStrc{};
int MONSTERS_GetHirelingTypeId(D2UnitStrc* p){return p?p->hire:0;}
int STATLIST_GetUnitBaseStat(D2UnitStrc* p,int,int){return p?p->resist:0;}
int STATLIST_UnitGetStatValue(D2UnitStrc* p,int stat,int){return stat==STAT_DEXTERITY?p->dex:stat==STAT_ARMORCLASS?p->defense:0;}
int STATLIST_UnitGetItemStatOrSkillStatValue(D2UnitStrc* p,int stat,int){return stat==STAT_SKILL_ARMOR_PERCENT?p->percent:0;}
bool STATES_CheckState(D2UnitStrc*,int){return false;}
D2StatListStrc* STATLIST_GetStatListFromUnitAndState(D2UnitStrc*,int){return nullptr;}
int STATLIST_GetStatValue(D2StatListStrc*,int,int){return 0;}
D2SkillsTxt* DATATBLS_GetSkillsTxtRecord(int){return nullptr;}
void INVENTORY_GetEquippedShield(void*,D2UnitStrc**){ }
int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int,int,int){return 0;}
'''
source+=function((ROOT/'.local/class-skills/SkillNec.cpp.reference').read_text(),'sub_6FD0B3D0')+'\n'
source+=function((ROOT/'.local/research/d2moo-CommonUnits.cpp').read_text(),'UNITS_GetDefense')+'\n'
source+=r'''
int main(){D2UnitStrc p;bool first=true;std::printf("{\"immunity\":[");
for(int kind=0;kind<3;kind++)for(int base:{0,80,99,100,110,200})for(int value:{-30,-125,-150,0,30})for(int stat:{STAT_FIRERESIST,STAT_SKILL_ARMOR_PERCENT}){
 p.dwUnitType=kind==1?UNIT_PLAYER:UNIT_MONSTER;p.hire=kind==2;p.resist=base;if(!first)std::printf(",");first=false;
 std::printf("[%d,%d,%d,%d,%d]",kind,base,value,stat,sub_6FD0B3D0(&p,stat,value));}
std::printf("],\"defense\":[");first=true;
for(int defense:{1,17,100,1001})for(int percent:{-49,-90,51,1,100}){p.defense=defense;p.percent=percent;if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d]",defense,percent,UNITS_GetDefense(&p));}
std::printf("]}\n");}
'''
(STAGE/'conviction-source-probe.cpp').write_text(source)
subprocess.run(['g++','-std=c++20','-O2',str(STAGE/'conviction-source-probe.cpp'),'-o',str(STAGE/'conviction-source-probe')],check=True)
out=subprocess.check_output([str(STAGE/'conviction-source-probe')]);(STAGE/'conviction-source-probe.json').write_bytes(out)
data=json.loads(out)
for kind,base,value,stat,actual in data['immunity']:
    expected=int(value/5) if kind==0 and base>=100 and value<=0 and stat==2 else value
    assert actual==expected,(kind,base,value,stat,actual)
for defense,percent,actual in data['defense']:assert actual==defense+int(defense*percent/100)
print('180 immunity/base-stat cases and20 signed defense cases passed; Holy Shield/override/event behavior explicitly outside probe.')
