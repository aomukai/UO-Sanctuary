#!/usr/bin/env python3
"""Execute complete corpse-explosion producer/callback and percentage arithmetic.

Target eligibility, base-stat lookup, RNG, elemental writer and area enumeration
are declared services. Both NPC312 and player74 rows use this older do55 body.
"""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills'
parser = argparse.ArgumentParser()
parser.add_argument('--check', action='store_true')
args = parser.parse_args()
preflight = json.loads((ROOT / 'tools/reference/death-sentry-preflight.json').read_text())
actor = preflight['actorSkills']['312']
assert [actor[key] for key in ['srvdofunc', 'Param1', 'Param2', 'Param3', 'Param4', 'Param5', 'EType']] == [55, 40, 80, 10, 1, 50, 'fire']
tree = json.loads((STAGE / 'd2moo-source-tree.json').read_text())
hashes, routines = {}, {}


def block(relative, name):
    raw = (STAGE / 'd2moo-pinned-source' / relative).read_bytes()
    assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(
        x['sha'] for x in tree['tree'] if x['path'] == relative)
    hashes[relative] = hashlib.sha256(raw).hexdigest()
    text = raw.decode()
    m = re.search(r'^.*\b' + name + r'\([^;\n]*\)\s*\n\{', text, re.M)
    assert m, name
    end, depth = text.index('{', m.start()) + 1, 1
    while depth:
        depth += (text[end] == '{') - (text[end] == '}')
        end += 1
    body = text[m.start():end]
    routines[name] = hashlib.sha256(body.encode()).hexdigest()
    return body


cpp = r'''
#include <cassert>
#include <cstdint>
#include <cstdio>
#include <cstring>
#include <initializer_list>
#define __fastcall
constexpr int UNIT_MONSTER=1,STAT_MAXHP=7,STAT_LEVEL=12,STATE_CORPSE_NODRAW=1;
struct Seed{};struct D2UnitStrc{int dwUnitType=1,dwClassId=0,level=1,x=100,y=100;Seed pSeed;};
struct D2GameStrc{int nGameType=0,dwGameType=0,nDifficulty=0;};
struct D2DamageStrc{int dwPhysDamage=0,dwFireDamage=0;};
struct D2MonStatsInitStrc{int nMinHP=0,nMaxHP=0;};
struct D2SkillsTxt{int dwAuraRangeCalc=4,dwCalc[3]={1,2,3},nEType=1,dwAuraFilter=3;}row;
struct D2UnkNecSkillStrc2{D2DamageStrc* pDamage=nullptr;int nX=0,nY=0,unk0x0C=0;};
struct D2AuraCallbackStrc{void* pArgs;D2GameStrc* pGame;D2UnitStrc* pOwner;};
D2UnitStrc caster,target,recipient;
bool targetPresent,usable,rowPresent;int hidden,refreshed,baseLife,minLife,maxLife,rangeValue,
 minPercent,maxPercent,elementPercent,rollKind,rollBound,areaCalls,areaRange,physical,fire,events,damageCalls;
D2UnitStrc* SUNIT_GetTargetUnit(D2GameStrc*,D2UnitStrc*){return targetPresent?&target:nullptr;}
bool D2COMMON_11021(D2UnitStrc* p){assert(p==&target);return usable;}
void STATES_ToggleState(D2UnitStrc* p,int state,int enabled){assert(p==&target&&state==1&&enabled==1);++hidden;}
void UNITROOM_RefreshUnit(D2UnitStrc* p){assert(p==&target);++refreshed;}
D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int id){assert(id==312);return rowPresent?&row:nullptr;}
int STATLIST_GetUnitBaseStat(D2UnitStrc* p,int stat,int){assert(p==&target&&stat==7);return baseLife;}
int STATLIST_UnitGetStatValue(D2UnitStrc* p,int stat,int){assert(stat==12);return p->level;}
void DATATBLS_CalculateMonsterStatsByLevel(int,bool,int difficulty,int level,int flag,D2MonStatsInitStrc* out){
 assert(difficulty>=0&&difficulty<=2&&level==target.level&&flag==1);out->nMinHP=minLife;out->nMaxHP=maxLife;}
int CLIENTS_GetUnitX(D2UnitStrc* p){return p->x;}int CLIENTS_GetUnitY(D2UnitStrc* p){return p->y;}
int SKILLS_EvaluateSkillFormula(D2UnitStrc* p,int calc,int id,int rank){assert(p==&caster&&id==312&&rank>0);
 switch(calc){case 1:return minPercent;case 2:return maxPercent;case 3:return elementPercent;case 4:return rangeValue;default:assert(false);return 0;}}
int ITEMS_RollLimitedRandomNumber(Seed* seed,int bound){assert(seed==&target.pSeed&&bound>=0);rollBound=bound;
 return bound==0?0:rollKind==0?0:rollKind==1?bound/2:bound-1;}
int SKILLS_GetElementalLength(D2UnitStrc*,int,int,int){return 0;}
void sub_6FD11E40(D2UnitStrc*,D2DamageStrc* damage,int element,int amount,int,int*,int*){
 assert(element==1);damage->dwFireDamage+=amount;}
int PATH_ComputeSquaredDistance(int x,int y,int x2,int y2){return (x-x2)*(x-x2)+(y-y2)*(y-y2);}
void SUNITDMG_ExecuteEvents(D2GameStrc*,D2UnitStrc* owner,D2UnitStrc* enemy,int kind,D2DamageStrc*){
 assert(owner==&caster&&enemy==&recipient&&kind==1);++events;}
void SUNITDMG_ExecuteMissileDamage(D2GameStrc*,D2UnitStrc* owner,D2UnitStrc* enemy,D2DamageStrc* d){
 assert(owner==&caster&&enemy==&recipient);physical=d->dwPhysDamage;fire=d->dwFireDamage;++damageCalls;}
void sub_6FD0FE80(D2GameStrc* game,D2UnitStrc* owner,int x,int y,int range,int filter,
 int(*callback)(D2AuraCallbackStrc*,D2UnitStrc*),void* args,int,const char*,int){
 assert(x==target.x&&y==target.y&&filter==3);++areaCalls;areaRange=range;
 D2AuraCallbackStrc aura{args,game,owner};assert(callback(&aura,&recipient)==1);}
'''
cpp += block('source/D2Game/src/MONSTER/MonsterUnique.cpp', 'MONSTERUNIQUE_CalculatePercentage') + '\n'
for name in ['sub_6FD0D000', 'SKILLS_SrvDo055_CorpseExplosion']:
    cpp += block('source/D2Game/src/SKILLS/SkillNec.cpp', name) + '\n'
cpp += r'''
int main(){D2GameStrc game;int count=0;std::printf("[");
 for(int mask=0;mask<16;++mask)for(int rank:{1,2,3,20,255})for(int life:{100,101,1000})
 for(int level:{0,1,50,99})for(int roll:{0,1,2})for(int edge:{0,1,2}){
  targetPresent=mask&1;usable=mask&2;rowPresent=mask&4;target.dwUnitType=(mask&8)?1:0;
  caster.level=50;target.level=level;baseLife=life*256;minLife=life;maxLife=life+3;
  rangeValue=10+rank-1;minPercent=40;maxPercent=80;elementPercent=50;rollKind=roll;
  recipient.x=target.x+rangeValue/2+(edge-1);recipient.y=target.y;
  hidden=refreshed=areaCalls=areaRange=events=damageCalls=physical=fire=0;rollBound=-1;
  int result=SKILLS_SrvDo055_CorpseExplosion(&game,&caster,312,rank);
  bool valid=targetPresent&&usable;
  assert(hidden==(valid?1:0)&&refreshed==hidden);
  assert(result==(valid&&rowPresent?1:0)&&areaCalls==result&&events==result&&damageCalls==result);
  if(result){assert(areaRange==(rangeValue+1)/2&&rollBound>0);if(edge==2){assert(physical==0);}else{assert(physical==fire);}}
  std::printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",count?",":"",mask,rank,life,level,roll,edge,
   result,hidden,areaRange,rollBound,physical,fire,damageCalls);++count;
 }
 std::printf("]\n");}
'''
source = STAGE / 'corpse-explosion-source-probe.cpp'
source.write_text(cpp)
binary = source.with_suffix('')
subprocess.run(['g++', '-std=c++17', '-O1', '-g', '-Wall', '-Wextra', '-Werror',
                '-fsanitize=address,undefined', '-fno-sanitize-recover=all', str(source), '-o', str(binary)], check=True)
run = subprocess.run([str(binary)], capture_output=True)
(STAGE / 'corpse-explosion-sanitizer.log').write_bytes(run.stderr)
assert run.returncode == 0 and not run.stderr, run.stderr.decode()[-2000:]
cases = json.loads(run.stdout)
assert len(cases) == 8640
fixture = STAGE / 'corpse-explosion-source-probe.json'
fixture.write_text(json.dumps(cases, separators=(',', ':')) + '\n')
evidence = dict(
    referenceVersion='3.3.93847', legacyCommit=tree['sha'], sourceHashes=hashes, routineHashes=routines,
    fixture=dict(path=str(fixture.relative_to(ROOT)), sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(), cases=len(cases)),
    established=[
        'Complete do55 marks the corpse no-draw and refreshes it before skill-row validation; a missing row can consume/hide a valid corpse without producing damage.',
        'Monster base life uses (minHP+maxHP)<<7 from a level/difficulty base-stat service. Caster level below a nonzero corpse level scales the random damage down. The nonmonster base-stat branch is a service control: the real common corpse predicate rejects nonmonsters.',
        'One exclusive upper-bound random roll uses the corpse seed, before elemental/physical splitting. NPC312 supplies40/80percent and50percent fire; these percentages differ from Necromancer skill74.',
        'The complete recipient callback clears physical damage outside floor(auraRange/2) squared, while retaining elemental damage. The area producer separately requests ceil(auraRange/2); enumeration/filter eligibility is an explicit service.'
    ],
    limits=[
        'This compiles the complete older producer, recipient callback and integer percentage helper, not modern executable behavior. Current NPC312 data is linked separately in death-sentry-preflight.json.',
        'Target eligibility, current base-stat lookup, RNG/seed advancement, fire writer, area enumeration/filtering, event/proc processing and defenses remain declared services. Selected recipient positions include deliberate callback controls outside the requested outer radius.',
        'No native Death Sentry, learned input, retirement, corpse persistence or class completion is claimed. Existing Necromancer uses inclusive random whole percentages and a single half-radius for both elements; reuse requires resolving these source differences rather than copying that handler unchanged.'
    ], sanitizers='ASan/UBSan exit0 with empty stderr required', handlerEnabled=False)
path = ROOT / 'tools/reference/corpse-explosion-source-evidence.json'
if args.check:
    assert json.loads(path.read_text()) == evidence
else:
    path.write_text(json.dumps(evidence, indent=2) + '\n')
print('Corpse explosion:', len(cases), 'complete producer/callback contexts; ASan/UBSan clean; services and existing reuse gaps explicit')
