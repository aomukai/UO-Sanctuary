#!/usr/bin/env python3
"""Compile unchanged skill-plus-item pierce creation with varied RNG sequences."""
from pathlib import Path
import hashlib,json,subprocess
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'
tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
source='source/D2Game/src/MISSILES/Missiles.cpp';raw=(STAGE/'d2moo-pinned-source'/source).read_bytes()
assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==source)
text=raw.decode();start=text.index('            const int32_t nPierceValue = ');end=text.index('\n        }',start)
block=text[start:end]
cpp=r'''#include <algorithm>
#include <cstdint>
#include <cstdio>
#include <initializer_list>
enum {STAT_ITEM_PIERCE,STAT_SKILL_PIERCE,STAT_PIERCE_IDX};
struct Unit{int item,skill,budget;};struct Params{Unit* pOwner;};using D2SeedStrc=int;
int draws[4],calls=0;
int STATLIST_UnitGetItemStatOrSkillStatValue(Unit* p,int,int){return p->item;}
int STATLIST_UnitGetStatValue(Unit* p,int,int){return p->skill;}
int STATLIST_GetUnitBaseStat(Unit*,int,int){return 17;}
void SEED_InitLowSeed(int* seed,int value){*seed=value;}
int ITEMS_RollRandomNumber(int*){return draws[calls++];}
void STATLIST_SetUnitStat(Unit* p,int,int value,int){p->budget=value;}
int pierce(int skill,int item){Unit owner{item,skill,0},missile{};Params params{&owner};auto* missileParams=&params;auto* pMissile=&missile;
'''+block+r'''
return missile.budget;}
int main(){bool first=true;std::printf("[");
for(int skill:{0,23,34,85,86,87})for(int item:{0,33,50,100})for(int pattern=0;pattern<6;pattern++){
 int patterns[6][4]={{99,99,99,99},{0,99,99,99},{0,0,99,99},{0,0,0,99},{0,0,0,0},{22,33,55,86}};
 for(int i=0;i<4;i++)draws[i]=patterns[pattern][i];calls=0;int budget=pierce(skill,item);
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d]",skill,item,pattern,budget,calls);
}std::printf("]\n");}
'''
path=STAGE/'amazon-pierce-source-probe.cpp';path.write_text(cpp);binary=STAGE/'amazon-pierce-source-probe'
subprocess.run(['g++','-std=c++20','-O2',str(path),'-o',str(binary)],check=True)
data=json.loads(subprocess.check_output([str(binary)]));assert len(data)==144
patterns=[[99,99,99,99],[0,99,99,99],[0,0,99,99],[0,0,0,99],[0,0,0,0],[22,33,55,86]]
for skill,item,pattern,budget,calls in data:
    expected=count=0
    if skill+item:
        for draw in patterns[pattern]:
            count+=1
            if draw>=skill+item:break
            expected+=1
    assert (budget,calls)==(expected,count)
fixture=STAGE/'amazon-pierce-source-probe.json';fixture.write_text(json.dumps(data,separators=(',',':'))+'\n')
collision='source/D2Game/src/MISSILES/MissMode.cpp';collision_raw=(STAGE/'d2moo-pinned-source'/collision).read_bytes()
assert hashlib.sha1(b'blob '+str(len(collision_raw)).encode()+b'\0'+collision_raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==collision)
c=collision_raw.decode();assert 'if (gMissileSrvDoFuncTable[pMissilesTxtRecord->wSrvDoFunc](pGame, pMissile) == 2)' in c
assert 'STATLIST_UnitGetStatValue(pOwner, STAT_SKILL_PIERCE, 0) || STATLIST_UnitGetItemStatOrSkillStatValue(pOwner, STAT_ITEM_PIERCE, 0)' in c
e=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],sourceHashes={source:hashlib.sha256(raw).hexdigest(),collision:hashlib.sha256(collision_raw).hexdigest()},
 probe=dict(cases=144,dataPath=str(fixture.relative_to(ROOT)),dataSha256=hashlib.sha256(fixture.read_bytes()).hexdigest(),unchangedCreationBlock=block,blockSha256=hashlib.sha256(block.encode()).hexdigest()),
 pinnedSkill=json.loads((ROOT/'tools/reference/amazon-pierce-preflight.json').read_text())['pinnedSkill'],
 officialGuide=dict(url='https://classic.battle.net/diablo2exp/skills/amazon-passive.shtml',rank1To20=[23,34,42,49,55,59,63,65,69,71,73,75,77,79,80,82,82,83,84,85]),
 established=['Factory adds item and skill pierce, initializes a separate seed from the owner pierce-index base stat and prerolls until first failure or4 successes. Varied draws cover budgets0/1/2/3/4 and exact RNG call counts; chance0 consumes no draw.',
  'Reviewed ordinary collision separately checks current positive owner item OR skill pierce before spending the saved budget. A missed AR roll returns2, and the outer dispatcher removes the missile on2; source return1 means retain. Do not invert this protocol.',
  'Pierce is a global passive stat; removing the equipped bow does not remove learned Pierce. Missile flags determine eligibility. The current native weapon component is a direct-damage pierce-capable missile adapter, not every spell or skill.'],
 services=['RNG supplies six declared four-draw sequences; stat/item services supply nonnegative skill and item values. Actual seed evolution and the owner pierce-index seed are not implemented by this probe.', 'Creation arithmetic compiles unchanged. Collision guards/dispatcher are source-reviewed, not compiled as a complete combat engine.'],
 limits=['No modern executable certificate; current RNG/seed sharing across a fan, missile flags, owner/special modes, old immediate-ray launch migration and graphical client acceptance remain open.', 'Official rank table supports the unchanged native diminishing model for1..20. Higher-rank reference rounding conflict remains unresolved; no curve change is made.'])
(ROOT/'tools/reference/amazon-pierce-engine-evidence.json').write_text(json.dumps(e,indent=2)+'\n')
print('Pierce:144 unchanged skill+item creation cases and exact RNG call counts passed; collision return protocol traced.')
