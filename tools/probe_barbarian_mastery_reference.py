#!/usr/bin/env python3
"""Compile unchanged pinned mastery selector and critical block with bounded stubs."""
from pathlib import Path
import hashlib,json,subprocess
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills'
BASE=STAGE/'d2moo-pinned-source/source'
FILES={
 'D2Common/src/D2Skills.cpp':'4c4610e8facb593c6c96567cef7ff5bfaf7d899f1869278977130111c8a58639',
 'D2Game/src/UNIT/SUnitDmg.cpp':'56ab5e14f7aceb18681f1c1e720e89c63bfe2a25e2e41b8aa9907652a7c9daf7'}
for rel,sha in FILES.items():assert hashlib.sha256((BASE/rel).read_bytes()).hexdigest()==sha
skills=(BASE/'D2Common/src/D2Skills.cpp').read_text()
start=skills.index('int __stdcall SKILLS_GetWeaponMasteryBonus(')
end=skills.index('\n//D2Common.',start)
selector=skills[start:end]
damage=(BASE/'D2Game/src/UNIT/SUnitDmg.cpp').read_text()
start=damage.index('\tif (!(pDamage->dwHitFlags & DAMAGEHITFLAG_1))')
end=damage.index('\n\tif (nSrcDam == 128)',start)
critical=damage[start:end]
cpp=r'''#include <algorithm>
#include <cstdint>
#include <cstdio>
#define __stdcall
#define ARRAY_SIZE(a) (sizeof(a)/sizeof(a[0]))
using BOOL=int;
constexpr int FALSE=0,STAT_PASSIVE_MASTERY_MELEE_TH=0,STAT_PASSIVE_MASTERY_MELEE_DMG=1,STAT_PASSIVE_MASTERY_MELEE_CRIT=2;
constexpr int STAT_PASSIVE_CRITICAL_STRIKE=3,STAT_ITEM_DEADLYSTRIKE=4,DAMAGEHITFLAG_1=1,DAMAGERESULTFLAG_CRITICALSTRIKE=2;
struct D2SkillStrc{};
struct D2StatStrc{int nLayer=0,nValue=0;};
struct D2UnitStrc{int pSeed=0,types=0,stats[5]={},roll[3]={},calls=0;D2StatStrc layers[3][4]={};};
D2SkillStrc* UNITS_GetUsedSkill(D2UnitStrc*){return nullptr;}
int D2Common_11024(D2UnitStrc*,D2UnitStrc*,D2SkillStrc*,int,BOOL* b){*b=0;return 0;}
int STATLIST_CopyStats(D2UnitStrc* p,int stat,D2StatStrc* out,int capacity){for(int i=0;i<4;i++)out[i]=p->layers[stat][i];return 4;}
int ITEMS_CheckItemTypeId(D2UnitStrc* p,int type){return (p->types & (1<<type))!=0;}
'''+selector+r'''
struct D2DamageStrc{int dwHitFlags=0,dwPhysDamage=77,dwEnDmgPct=0,wResultFlags=0,dwFireDamage=13;};
D2UnitStrc weapon;
D2UnitStrc* sub_6FC7C7B0(D2UnitStrc*){return &weapon;}
int SUNITDMG_ApplyDamageBonuses(D2UnitStrc*,int,void*,int,int,int,int,int){return 100;}
int ITEMS_RollRandomNumber(int* seed){auto* p=reinterpret_cast<D2UnitStrc*>(seed);return p->roll[p->calls++];}
int STATLIST_UnitGetStatValue(D2UnitStrc* p,int s,int){return p->stats[s];}
int STATLIST_UnitGetItemStatOrSkillStatValue(D2UnitStrc* p,int s,int){return p->stats[s];}
void critical(D2UnitStrc* pAttacker,D2DamageStrc* pDamage,int a5){int nSrcDam=128;
'''+critical+r'''
}
int main(){bool first=true;std::printf("{\"selectorCases\":[");
for(int types:{0,2,4,6})for(int stat:{0,1,2,3}){
 D2UnitStrc p,item;item.types=types;
 for(int s=0;s<3;s++){p.layers[s][0]={1,10+s};p.layers[s][1]={1,30+s};p.layers[s][2]={2,20+s};p.layers[s][3]={3,999};}
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d]",types,stat,SKILLS_GetWeaponMasteryBonus(&p,&item,nullptr,stat));
}D2UnitStrc p;std::printf("],\"guards\":[%d,%d],\"criticalCases\":[",SKILLS_GetWeaponMasteryBonus(nullptr,&p,nullptr,0),SKILLS_GetWeaponMasteryBonus(&p,nullptr,nullptr,0));first=true;
for(int mastery:{0,29,100})for(int cs:{0,20,100})for(int deadly:{0,35,100})for(int roll:{0,28,29,34,35,99})for(int ranged:{0,1})for(int skip:{0,1}){
 D2UnitStrc p;weapon.types=2;p.layers[2][0]={1,mastery};p.stats[3]=cs;p.stats[4]=deadly;p.roll[0]=p.roll[1]=p.roll[2]=roll;
 D2DamageStrc d;d.dwHitFlags=skip;critical(&p,&d,ranged);
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",mastery,cs,deadly,roll,ranged,skip,d.dwPhysDamage,d.wResultFlags,p.calls,d.dwFireDamage);
}std::printf("]}\n");}
'''
path=STAGE/'barbarian-mastery-source-probe.cpp';path.write_text(cpp)
binary=STAGE/'barbarian-mastery-source-probe'
subprocess.run(['g++','-std=c++20','-O2',str(path),'-o',str(binary)],check=True)
data=json.loads(subprocess.check_output([str(binary)]))
assert data['guards']==[0,0]
for types,stat,value in data['selectorCases']:
 assert value==(max(([30+stat] if types&2 else [])+([20+stat] if types&4 else [])+[0]) if stat<3 else 0)
for mastery,cs,deadly,roll,ranged,skip,physical,flags,calls,fire in data['criticalCases']:
 checks=[] if skip else ([mastery] if not ranged and mastery>0 else [])+([cs] if cs>0 else [])+([deadly] if deadly>0 else [])
 expected_calls=0;hit=False
 for chance in checks:
  expected_calls+=1
  if roll<chance:hit=True;break
 assert (physical,flags,calls,fire)==(77 if skip else 200 if hit else 100,2 if hit else 0,expected_calls,13)
(STAGE/'barbarian-mastery-source-probe.json').write_text(json.dumps(data,indent=2)+'\n')
evidence={'referenceVersion':'3.3.93847','legacyCommit':'0c0bbd1e6bef9181a3da1c046523af622a925416','sourceFiles':FILES,
 'probe':{'path':'.local/class-skills/barbarian-mastery-source-probe.json','selectorCases':len(data['selectorCases']),'criticalCases':len(data['criticalCases']),'guards':2,
 'selectorSha256':hashlib.sha256(selector.encode()).hexdigest(),'criticalBlockSha256':hashlib.sha256(critical.encode()).hexdigest(),
 'stubs':['Statlist layers and item-type membership supplied explicitly; modern type inheritance is separately checked against pinned itemtypes.json.','Throw routing returns no throw bonus; throwing mastery is excluded from this probe/implementation.','ApplyDamageBonuses supplies100 physical; damage pooling is a separate native check.','RNG sequence, generic critical/deadly stats and equipped item are explicit stubs.']},
 'established':['Strongest eligible item-type-layer mastery, not sum; null unit/item and invalid stat return0.','Melee mastery critical is tried first, then generic critical, then deadly. A successful roll short-circuits; only physical doubles once.','The source ranged flag skips melee mastery critical. The hit flag skips this whole ordinary physical fill block.'],
 'limits':['Legacy compiled isolation does not certify the current D2R engine.','Generic Critical Strike is not yet a supported Sanctuary skill; the source branch is tested only in isolation.','Dual wield and melee use of converted throwing items require separate adapters; Throwing Mastery modern pierce/quantity/replenish remain unimplemented.']}
(ROOT/'tools/reference/barbarian-mastery-engine-evidence.json').write_text(json.dumps(evidence,indent=2)+'\n')
print(f"Masteries: {len(data['selectorCases'])} selectors, two guards, {len(data['criticalCases'])} unchanged critical-block cases passed.")
