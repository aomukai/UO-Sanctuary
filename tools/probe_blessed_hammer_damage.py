#!/usr/bin/env python3
"""Execute unchanged Hammer fill/roll/damage/Concentration/start wrappers."""
import hashlib,json,re,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/blessed-hammer-contact/damage-source'
COMMIT='0c0bbd1e6bef9181a3da1c046523af622a925416'
def routine(text,name):
 match=re.search(r'(?m)^(?:int32_t|int|void) (?:__fastcall|__stdcall) '+re.escape(name)+r'\(',text);assert match,name
 start=at=match.start();end=text.index('{',at)+1;depth=1
 while depth:depth+=(text[end]=='{')-(text[end]=='}');end+=1
 return text[start:end]
def main():
 paths={
  'miss':('.local/class-skills/MissMode.cpp.reference','source/D2Game/src/MISSILES/MissMode.cpp'),
  'skill':('.local/class-skills/SkillPal.cpp.reference','source/D2Game/src/SKILLS/SkillPal.cpp'),
  'common':('.local/research/d2moo-CommonSkills.cpp','source/D2Common/src/D2Skills.cpp'),
  'percentage':('.local/class-skills/d2moo-pinned-source/source/D2Game/src/MONSTER/MonsterUnique.cpp','source/D2Game/src/MONSTER/MonsterUnique.cpp')}
 tree=json.loads((ROOT/'.local/class-skills/d2moo-source-tree.json').read_text());assert tree['sha']==COMMIT and not tree['truncated'];entries={x['path']:x['sha'] for x in tree['tree']}
 source={};hashes={}
 for key,(name,upstream) in paths.items():
  data=(ROOT/name).read_bytes();assert hashlib.sha1(b'blob '+str(len(data)).encode()+b'\0'+data).hexdigest()==entries[upstream];source[key]=data.decode();hashes[name]=hashlib.sha256(data).hexdigest()
 functions=[('percentage','MONSTERUNIQUE_CalculatePercentage'),('miss','MISSMODE_RollDamageValue'),('miss','MISSMODE_FillDamageParams'),('miss','MISSMODE_GetDamageValue'),('miss','MISSMODE_AddDamageValue'),('miss','MISSMODE_SrvDmg05_BlessedHammer'),('common','SKILLS_GetConcentrationDamageBonus'),('skill','SKILLS_SrvDo073_BlessedHammer')]
 bodies='\n'.join(routine(source[k],n) for k,n in functions)
 stats=sorted(set(re.findall(r'\bSTAT_[A-Z0-9_]+\b',bodies)));elements=sorted(set(re.findall(r'\bELEMTYPE_[A-Z0-9_]+\b',bodies)))
 constants='enum{'+','.join(stats+elements)+',STAT_COUNT};\n'
 fields=sorted(set(re.findall(r'pDamage->([A-Za-z0-9_]+)',bodies)))
 scaffold=r'''#include <algorithm>
#include <cstdint>
#include <cstdio>
#include <cstring>
#include <iterator>
#include <vector>
#define __fastcall
#define __stdcall
constexpr int UNIT_MONSTER=1,DAMAGERESULTFLAG_CRITICALSTRIKE=8,DAMAGEHITFLAG_BYPASSUNDEAD=256,DAMAGEHITFLAG_BYPASSDEMON=512,DAMAGEHITFLAG_BYPASSBEASTS=1024,STATE_CONCENTRATION=1,UNITFLAG_SKSRVDOFUNC=64,PATHTYPE_BLESSEDHAMMER=16;
struct D2GameStrc{};struct D2DynamicPathStrc{};struct D2StatListStrc{};
struct D2UnitStrc{int dwClassId=77,dwUnitType=1,dwFlags=0,pSeed=0;D2DynamicPathStrc* pDynamicPath=nullptr;};
struct D2SkillsTxt{int dwParam[1]={4};};struct D2MissilesTxt{int dwDmgParam[2]={50,50};};
struct D2MonStatsTxt{int wMonType=0;};struct D2StatStrc{int nLayer,nValue;};
struct Tables{int nMonTypeTxtRecordCount=1,nMonTypeIndex=1;uint32_t* pMonTypeNest=nullptr;} tables;Tables* sgptDataTables=&tables;uint32_t gdwBitMasks[32]={};
struct D2MissileStrc{int dwFlags=0;D2UnitStrc* pOwner=nullptr;D2UnitStrc* pOrigin=nullptr;int nMissile=0,nTargetX=0,nTargetY=0,nSkill=0,nSkillLevel=0;};
D2SkillsTxt skillRow;D2MissilesTxt missileRow;D2MonStatsTxt monsterRow;D2StatListStrc aura;D2UnitStrc missile;
bool demon=false,undead=false,hasMissileRow=true,hasSkill=true,hasState=true,hasList=true,hasTarget=true,created=true;
int auraPercent=60,progressive=97,pattern=0,rngCalls=0,draws=0,createCalls=0,pathTypes=0,paths=0,writes=0,createdFlags=0;
int STATS[256]={};
int STATLIST_UnitGetStatValue(D2UnitStrc*,int stat,int){return STATS[stat];}
int STATLIST_GetUnitBaseStat(D2UnitStrc*,int stat,int){return STATS[stat];}
void STATLIST_SetUnitStat(D2UnitStrc*,int stat,int value,int){STATS[stat]=value;++writes;}
int ITEMS_RollLimitedRandomNumber(int*,int bound){int ordinal=rngCalls++;if(bound<=0)return 0;++draws;return (pattern&(1<<(ordinal&1)))?bound-1:0;}
bool MONSTERS_IsDemon(D2UnitStrc*){return demon;}bool MONSTERS_IsUndead(D2UnitStrc*){return undead;}
D2MonStatsTxt* MONSTERMODE_GetMonStatsTxtRecord(int){return &monsterRow;}
int STATLIST_CopyStats(D2UnitStrc*,int,D2StatStrc*,size_t){return 0;}
D2MissilesTxt* SKILLS_GetMissilesTxtRecord(int){return hasMissileRow?&missileRow:nullptr;}
int MISSILE_GetElemTypeFromMissileId(int){return ELEMTYPE_MAGIC;}
bool STATES_CheckState(D2UnitStrc*,int){return hasState;}
D2StatListStrc* STATLIST_GetStatListFromUnitAndState(D2UnitStrc*,int){return hasList?&aura:nullptr;}
D2SkillsTxt* DATATBLS_GetSkillsTxtRecord(int){return hasSkill?&skillRow:nullptr;}
D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int){return hasSkill?&skillRow:nullptr;}
int STATLIST_GetStatValue(D2StatListStrc*,int,int){return auraPercent;}
int SKILLS_GetProgressiveSkillMissileId(D2UnitStrc*,int){return progressive;}
bool D2GAME_GetXAndYFromTargetUnit_6FD14020(D2GameStrc*,D2UnitStrc*,int* x,int* y){*x=12;*y=34;return hasTarget;}
D2UnitStrc* MISSILES_CreateMissileFromParams(D2GameStrc*,D2MissileStrc* p){++createCalls;createdFlags=p->dwFlags;return created?&missile:nullptr;}
void PATH_SetType(D2DynamicPathStrc*,int){++pathTypes;}void D2Common_10142(D2DynamicPathStrc*,D2UnitStrc*,int){++paths;}
void reset(){std::memset(STATS,0,sizeof(STATS));demon=undead=false;hasMissileRow=hasSkill=hasState=hasList=hasTarget=created=true;auraPercent=60;progressive=97;pattern=rngCalls=draws=createCalls=pathTypes=paths=writes=createdFlags=0;skillRow={};missileRow={};missile={};}
'''
 # Enumerated identifiers and the complete damage structure precede services.
 cpp='#include <cstdint>\n'+constants+'struct D2DamageStrc{'+''.join('int32_t '+n+';' for n in fields)+'};\n'+scaffold+'\n'+bodies
 harness=r'''
int main(){D2GameStrc game;D2UnitStrc target;bool first=true;printf("{\"damage\":[");
for(auto bounds:{std::pair{2560,2816},std::pair{5111,6814},std::pair{331161,337920},std::pair{2560,2560},std::pair{2816,2560},std::pair{0,2816},std::pair{1200059,1200899}})for(int mastery:{0,25,-25})for(int d=0;d<2;++d)for(int u=0;u<2;++u)for(int pat=0;pat<4;++pat){reset();STATS[STAT_MAGICMINDAM]=bounds.first;STATS[STAT_MAGICMAXDAM]=bounds.second;STATS[STAT_PASSIVE_MAG_MASTERY]=mastery;demon=d;undead=u;pattern=pat;D2DamageStrc packet{};MISSMODE_FillDamageParams(&missile,&target,&packet);int fill=packet.dwMagDamage;MISSMODE_SrvDmg05_BlessedHammer(&game,&missile,&target,&packet);printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",bounds.first,bounds.second,mastery,d,u,pat,fill,packet.dwMagDamage,rngCalls,draws);first=false;}
printf("],\"concentration\":[");first=true;
for(int pct:{0,1,59,60,61,345,1000})for(int absent=0;absent<4;++absent){reset();auraPercent=pct;if(absent==1)hasState=false;if(absent==2)hasList=false;if(absent==3)hasSkill=false;int value=SKILLS_GetConcentrationDamageBonus(&missile,112);printf("%s[%d,%d,%d]",first?"":",",pct,absent,value);first=false;}
printf("],\"start\":[");first=true;
for(int pct:{0,60,61,345})for(int guard=0;guard<5;++guard){reset();auraPercent=pct;STATS[STAT_MAGICMINDAM]=3932;STATS[STAT_MAGICMAXDAM]=5242;if(guard==1)hasSkill=false;if(guard==2)progressive=0;if(guard==3)hasTarget=false;if(guard==4)created=false;int value=SKILLS_SrvDo073_BlessedHammer(&game,&missile,112,1);printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",pct,guard,value,createCalls,createdFlags,pathTypes,paths,writes,STATS[STAT_MAGICMINDAM],STATS[STAT_MAGICMAXDAM]);first=false;}
printf("]}\n");}
'''
 STAGE.mkdir(parents=True,exist_ok=True);path=STAGE/'hammer-damage.cpp';binary=STAGE/'hammer-damage';path.write_text(cpp+'\n'+harness)
 subprocess.run(['g++','-std=c++17','-O2','-Wall','-Wextra','-Wno-unused-parameter',str(path),'-o',str(binary)],check=True)
 cases=json.loads(subprocess.check_output([str(binary)]));assert len(cases['damage'])==336 and len(cases['concentration'])==28 and len(cases['start'])==20
 trunc=lambda n,d:abs(n)//d*(-1 if n<0 else 1)
 def percent(value,pct):return pct*trunc(value,100) if value>1048576 else trunc(value*pct,100)
 for lo,hi,mastery,d,u,pat,fill,total,calls,draws in cases['damage']:
  if lo<=0 or hi<=0:expected_fill=expected_total=0;expected_calls=expected_draws=0
  else:
   low,high=sorted((lo,hi));low+=percent(low,mastery);high+=percent(high,mastery);span=high-low
   expected_fill=low+(span-1 if pat&1 and span>0 else 0);base=low+(span-1 if pat&2 and span>0 else 0);expected_total=base+percent(base,50)*(d+u);expected_calls=2;expected_draws=2 if span>0 else 0
  assert (fill,total,calls,draws)==(expected_fill,expected_total,expected_calls,expected_draws)
 for pct,absent,value in cases['concentration']:assert value==(0 if absent else pct*4//8)
 for pct,guard,value,creates,flags,path_types,path_calls,writes,lo,hi in cases['start']:
  concentration=pct*4//8;success=guard==0
  assert (value,creates,flags,path_types,path_calls,writes)==(int(success),int(guard in (0,4)),32 if guard in (0,4) else 0,int(success),int(success),2 if success and concentration else 0)
  assert (lo,hi)==((3932*(100+concentration)//100,5242*(100+concentration)//100) if success else (3932,5242))
 out=STAGE/'cases.json';out.write_text(json.dumps(cases,indent=2)+'\n')
 result=dict(referenceVersion='3.3.93847',sourceCommit=COMMIT,completeFunctions=[n for _,n in functions],sourceHashes=hashes,counts={k:len(v) for k,v in cases.items()},
  declaredServices=['Table/state/species/stat services are controlled callbacks;only magic endpoints/mastery are populated. Other element/physical branches compile unchanged but are not claimed as covered.','Limited RNG supplies alternating lower/exclusive-upper endpoints;callback calls and positive-bound draws are recorded separately. No current engine seed stream is claimed.','The complete percentage routine executes,including its large-value early division. Extreme signed32overflow remains outside these bounded cases.','Factory/path functions record flags32,creation/failed guards and path calls;they do not execute world allocation or Fog/path geometry.'],
  established=['Fill then damage5consume two positive-bound magic draws,with the second replacing the first. Equal bounds invoke limited helper twice without positive-bound draws;nonpositive endpoints return zero without RNG.','Undead/demon bonuses each derive from the rerolled base and add independently. Concentration uses aura damagepercent*Param1/8,and do73applies it to factory base magic endpoints after missile creation.'],
  remaining=['Modern executable/event/critical/special-mode behavior,complete nested stat/RNG/factory/world/path services and full native acceptance remain separate.'],
  artifacts={str(p.relative_to(ROOT)):hashlib.sha256(p.read_bytes()).hexdigest() for p in (Path(__file__).resolve(),path,binary,out)})
 (ROOT/'tools/reference/blessed-hammer-damage-engine-evidence.json').write_text(json.dumps(result,indent=2)+'\n')
 print('Blessed Hammer complete source wrappers:336damage/28Concentration/20start cases pass;two-draw order/species/launch snapshot verified with declared services.')
if __name__=='__main__':main()
