#!/usr/bin/env python3
"""Compose unchanged source critical stages and independently verify saved RNG."""
import hashlib,json,os,subprocess
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1]
CACHE=Path(os.environ.get('SANCTUARY_REFERENCE_CACHE',ROOT/'.local/class-skills'))
STAGE=ROOT/'.local/class-skills/npc-equipment-critical-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build():
 STAGE.mkdir(parents=True,exist_ok=True);tree=json.loads((CACHE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416';inputs={};routines={}
 def read(name):
  p=CACHE/'d2moo-pinned-source'/name;raw=p.read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==name);inputs[str(p)]=sha(p);return raw.decode()
 damage=read('source/D2Game/src/UNIT/SUnitDmg.cpp');mode=read('source/D2Game/src/MONSTER/MonsterMode.cpp');header=read('source/D2Game/include/UNIT/SUnitDmg.h')
 assert 'DAMAGERESULTFLAG_CRITICALSTRIKE = 0x00002000' in header
 start=damage.index('\tif (!(pDamage->dwHitFlags & DAMAGEHITFLAG_1))');end=damage.index('\n\tif (nSrcDam == 128)',start);lane=damage[start:end]
 cpp=r'''#include <cassert>
#include <cstdint>
#include <cstdio>
#include <initializer_list>
#define __fastcall
#define UNIT_MONSTER 1
constexpr int DAMAGEHITFLAG_1=1,DAMAGERESULTFLAG_CRITICALSTRIKE=0x2000,STAT_PASSIVE_CRITICAL_STRIKE=337,STAT_ITEM_DEADLYSTRIKE=141;
struct D2SeedStrc{};struct D2UnitStrc{int dwUnitType=1,dwClassId=0;D2SeedStrc pSeed;};
struct D2DamageStrc{int dwPhysDamage=257,dwFireDamage=23,dwLtngDamage=37,dwMagDamage=53,dwColdDamage=71,dwPoisDamage=769,dwColdLen=47,dwPoisLen=103,dwEnDmgPct=0;uint32_t dwHitFlags=0,wResultFlags=0,dwHitClass=0;};
struct D2MonStatsTxt{int nCrit=0;}mon;
int mastery=0,generic=0,deadly=0,draws=0,style=0,overlay=0,physicalCalls=0;
uint32_t ITEMS_RollRandomNumber(D2SeedStrc*){assert(draws<4);const uint32_t values[4][4]={{0,0,0,0},{99,0,99,0},{35,50,45,5},{99,99,99,99}};return values[style][draws++];}
int SUNITDMG_ApplyDamageBonuses(D2UnitStrc*,int,D2UnitStrc*,int,int,int,int,int){physicalCalls++;return 513;}
D2UnitStrc*sub_6FC7C7B0(D2UnitStrc*p){return p;}
int SKILLS_GetWeaponMasteryBonus(D2UnitStrc*,D2UnitStrc*,int,int){return mastery;}
int STATLIST_UnitGetStatValue(D2UnitStrc*,int stat,int){assert(stat==STAT_PASSIVE_CRITICAL_STRIKE);return generic;}
int STATLIST_UnitGetItemStatOrSkillStatValue(D2UnitStrc*,int stat,int){assert(stat==STAT_ITEM_DEADLYSTRIKE);return deadly;}
D2MonStatsTxt*MONSTERMODE_GetMonStatsTxtRecord(int){return &mon;}
void UNITS_SetOverlay(D2UnitStrc*p,int value,int z){assert(p&&z==0);overlay=value;}
'''
 for src,name in [(damage,'SUNITDMG_SetHitClass'),(mode,'D2GAME_MONSTER_ApplyCriticalDamage_6FC62E70')]:
  body=function(src,name);routines[name]=hashlib.sha256(body.encode()).hexdigest();cpp+=body+'\n'
 cpp+='void Physical(D2UnitStrc*pAttacker,D2DamageStrc*pDamage,int a5,int nSrcDam){\n'+lane+'\n}\n'
 cpp+=r'''int main(){printf("[\n");bool first=true;D2UnitStrc actor,target;
for(int skip:{0,1})for(int ranged:{0,1})for(int m:{0,35,100})for(int g:{0,50})for(int d:{0,45,100})for(int intrinsic:{0,5,100})for(int roll:{0,1,2,3})for(int hit:{3,0x63})for(int defender:{0,1}){
mastery=m;generic=g;deadly=d;mon.nCrit=intrinsic;style=roll;draws=overlay=physicalCalls=0;D2DamageStrc packet;packet.dwHitFlags=skip;packet.dwHitClass=hit;
Physical(&actor,&packet,ranged,128);int before=draws;D2GAME_MONSTER_ApplyCriticalDamage_6FC62E70(&actor,defender?&target:nullptr,&packet);
printf("%s[[%d,%d,%d,%d,%d,%d,%d,%d,%d],[%d,%d,%d,%d,%d,%d,%d,%d,%u,%d,%d,%d,%d,%d]]",first?"":",\n",skip,ranged,m,g,d,intrinsic,roll,hit,defender,packet.dwPhysDamage,packet.dwFireDamage,packet.dwLtngDamage,packet.dwMagDamage,packet.dwColdDamage,packet.dwPoisDamage,packet.dwColdLen,packet.dwPoisLen,packet.dwHitClass,overlay,(packet.wResultFlags&DAMAGERESULTFLAG_CRITICALSTRIKE)!=0,before,draws,physicalCalls);first=false;
}printf("\n]\n");}
'''
 (STAGE/'critical.cpp').write_text(cpp)
 seedh=read('source/D2Common/include/D2Seed.h');seedcpp=read('source/D2Common/src/D2Seed.cpp');items=read('source/D2Game/src/ITEMS/Items.cpp')
 rng=r'''#include <cassert>
#include <cstdint>
#include <cstdio>
#include <initializer_list>
#define __fastcall
#define __stdcall
#pragma GCC diagnostic ignored "-Wliteral-suffix"
constexpr long long operator""i64(unsigned long long v){return static_cast<long long>(v);}
struct D2SeedStrc{union{struct{uint32_t nLowSeed,nHighSeed;};uint64_t lSeed;};};
'''
 for src,names in [(seedh,['SEED_RollRandomNumber','SEED_RollLimitedRandomNumber']),(seedcpp,['SEED_InitLowSeed']),(items,['ITEMS_RollRandomNumber','ITEMS_RollLimitedRandomNumber'])]:
  for name in names:
   body=function(src,name);routines[name]=hashlib.sha256(body.encode()).hexdigest();rng+=body+'\n'
 rng+=r'''int main(){printf("[\n");bool first=true;for(int low:{0,1,-1,1234,2147483647}){D2SeedStrc seed;SEED_InitLowSeed(&seed,low);
for(int step=0;step<30;step++){const int bounds[]={0,1,2,100,257,2147483647};int bound=bounds[step%6];uint32_t value=step%3==0?ITEMS_RollRandomNumber(&seed):ITEMS_RollLimitedRandomNumber(&seed,bound);
printf("%s[%d,%d,%d,%u,%llu]",first?"":",\n",low,step,step%3==0?-1:bound,value,static_cast<unsigned long long>(seed.lSeed));first=false;}}printf("\n]\n");}
'''
 (STAGE/'seed.cpp').write_text(rng)
 counts={}
 for name,count in [('critical',3456),('seed',150)]:
  run=subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-omit-frame-pointer',str(STAGE/(name+'.cpp')),'-o',str(STAGE/name)],capture_output=True,text=True);(STAGE/(name+'-compiler.log')).write_text(run.stdout+run.stderr);assert run.returncode==0,run.stderr
  run=subprocess.run([str(STAGE/name)],capture_output=True);(STAGE/(name+'-sanitizer.log')).write_bytes(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr
  cases=json.loads(run.stdout);assert len(cases)==count,(name,len(cases));(STAGE/(name+'-cases.json')).write_text(json.dumps(cases,indent=2)+'\n');counts[name]=count
 artifacts=[p for p in STAGE.iterdir() if p.suffix in ['.cpp','.json','.log']]
 result=dict(referenceVersion='3.3.93847',olderEngineCommit=tree['sha'],cases=counts,inputs=inputs,compiledUnchangedRoutineHashes=routines,
  unchangedPhysicalCriticalBlockSha256=hashlib.sha256(lane.encode()).hexdigest(),
  scope='Unchanged complete physical-critical statement block with declared damage/stat/RNG services; complete intrinsic critical/hit-class routines. Separate complete source seed initialization/roll/wrapper routines. Not complete FillDamageValues,weapon-mastery lookup,current executable or AI seed-allocation parity.',artifacts={str(p.relative_to(ROOT)):sha(p) for p in artifacts})
 (ROOT/'tools/reference/npc-equipment-critical-source-evidence.json').write_text(json.dumps(result,indent=2)+'\n');print('Source critical3456 and seed150 cases pass ASan/UBSan.')
if __name__=='__main__':build()
