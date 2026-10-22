#!/usr/bin/env python3
"""Compile unchanged legacy Holy Shield gate, block-animation speed and defense getter; explicit stats/formula stubs."""
from pathlib import Path
import hashlib,json,subprocess
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'
SOURCES={'.local/class-skills/SkillPal.cpp.reference':'c421857a633b5dd1f441eded1c5f41cbd88b375ba9dd771c3fa802e39591bdf0','.local/research/d2moo-CommonUnits.cpp':'a226a3d1f3fbe062a1ab6d8807c54058dcd6bc88bd3604b6fcdfad1d229554ea'}
for path,digest in SOURCES.items():
    if hashlib.sha256((ROOT/path).read_bytes()).hexdigest()!=digest:raise SystemExit('Pinned source missing/changed: '+path)
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
#define __forceinline inline
constexpr int STAT_ITEM_FASTERBLOCKRATE=1,STATE_HOLYSHIELD=2,STAT_DEXTERITY=3,STAT_ARMORCLASS=4,STAT_ITEM_ARMOR_PERCENT=5,STAT_SKILL_ARMOR_PERCENT=6,STAT_MODIFIERLIST_SKILL=7,STAT_MODIFIERLIST_LEVEL=8,STAT_ARMOR_OVERRIDE_PERCENT=9;
struct AnimData{int dwAnimSpeed=256;};struct D2GameStrc{};struct D2StatListStrc{};struct D2SkillsTxt{int dwCalc[1]={};};
struct D2UnitStrc{void* pInventory=nullptr;AnimData* pAnimData=nullptr;int16_t wAnimSpeed=0;int fbr=0,holy=0,shield=0,rank=1,defiance=1,defense=100;};
int STATLIST_UnitGetItemStatOrSkillStatValue(D2UnitStrc* p,int stat,int){return stat==STAT_ITEM_FASTERBLOCKRATE?p->fbr:0;}
int STATLIST_UnitGetStatValue(D2UnitStrc* p,int stat,int){return stat==STAT_ARMORCLASS?p->defense:0;}
bool STATES_CheckState(D2UnitStrc* p,int){return p->holy;}
int D2Clamp(int x,int a,int b){return std::clamp(x,a,b);}
D2StatListStrc* STATLIST_GetStatListFromUnitAndState(D2UnitStrc* p,int){return (D2StatListStrc*)p;}
int STATLIST_GetStatValue(D2StatListStrc* p,int stat,int){return stat==STAT_MODIFIERLIST_LEVEL?((D2UnitStrc*)p)->rank:117;}
D2SkillsTxt* DATATBLS_GetSkillsTxtRecord(int){static D2SkillsTxt s;return &s;}
int INVENTORY_GetEquippedShield(void* inventory,D2UnitStrc** shield){auto p=(D2UnitStrc*)inventory;if(shield)*shield=p&&p->shield?p:nullptr;return p&&p->shield;}
int SKILLS_EvaluateSkillFormula(D2UnitStrc* p,int,int,int level){return 25+(level-1)*15+p->defiance*15;}
'''
source+=function((ROOT/'.local/class-skills/SkillPal.cpp.reference').read_text(),'SKILLS_SrvSt36_HolyShield')+'\n'
units=(ROOT/'.local/research/d2moo-CommonUnits.cpp').read_text()
source+=function(units,'UNITS_UpdateBlockAnimRateAndVelocity')+'\n'+function(units,'UNITS_GetDefense')+'\n'
source+=r'''
int main(){D2UnitStrc p;AnimData a;p.pInventory=&p;p.pAnimData=&a;bool first=true;std::printf("{\"block\":[");
for(int holy:{0,1})for(int fbr:{0,12,13,32,40,85,86,100}){p.holy=holy;p.fbr=fbr;UNITS_UpdateBlockAnimRateAndVelocity(&p);if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d]",holy,fbr,p.wAnimSpeed);}
std::printf("],\"defense\":[");first=true;for(int shield:{0,1})for(int rank:{1,20})for(int hard:{1,2,20}){p.shield=shield;p.holy=1;p.rank=rank;p.defiance=hard;if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d]",shield,rank,hard,UNITS_GetDefense(&p));}
std::printf("],\"castGate\":[");for(int shield=0;shield<2;shield++){p.shield=shield;if(shield)std::printf(",");std::printf("%d",SKILLS_SrvSt36_HolyShield(nullptr,&p,117,1));}std::printf("]}\n");}
'''
(STAGE/'holy-shield-source-probe.cpp').write_text(source)
subprocess.run(['g++','-std=c++20','-O2',str(STAGE/'holy-shield-source-probe.cpp'),'-o',str(STAGE/'holy-shield-source-probe')],check=True)
out=subprocess.check_output([str(STAGE/'holy-shield-source-probe')]);(STAGE/'holy-shield-source-probe.json').write_bytes(out);data=json.loads(out)
assert data['castGate']==[0,1]
for holy,fbr,speed in data['block']:assert speed==256*((100 if holy else 50)+120*fbr//(120+fbr))//100
for shield,rank,hard,defense in data['defense']:assert defense==100+(25+(rank-1)*15+hard*15 if shield else 0)
print('Holy Shield:2 cast gates,16 original block-speed cases and12 defense cases pass; formula/state/inventory stubs explicit.')
