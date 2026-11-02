#!/usr/bin/env python3
"""Execute unchanged older SrvHit07; modern piercing/demon policy is distinct."""
import hashlib,json,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/holy-bolt-contact/source-probe'
SOURCE=ROOT/'.local/class-skills/MissMode.cpp.reference'
COMMIT='0c0bbd1e6bef9181a3da1c046523af622a925416'
UPSTREAM='source/D2Game/src/MISSILES/MissMode.cpp'
def main():
 evidence=json.loads((ROOT/'tools/reference/holy-bolt-evidence.json').read_text());blob=SOURCE.read_bytes()
 assert hashlib.sha256(blob).hexdigest()==evidence['sourceHashes'][str(SOURCE.relative_to(ROOT))]
 tree=json.loads((ROOT/'.local/class-skills/d2moo-source-tree.json').read_text());assert tree['sha']==COMMIT and not tree['truncated']
 assert hashlib.sha1(b'blob '+str(len(blob)).encode()+b'\0'+blob).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==UPSTREAM)
 text=blob.decode();name='MISSMODE_SrvHit07_HolyBolt_FistOfTheHeavenBolt';at=text.index(name+'(');start=text.rfind('\n',0,at)+1;end=text.index('{',at)+1;depth=1
 while depth:depth+=(text[end]=='{')-(text[end]=='}');end+=1
 routine=text[start:end]
 scaffold=r'''#include <algorithm>
#include <cstdint>
#include <cstdio>
#include <vector>
#define __fastcall
constexpr int UNIT_PLAYER=0,UNIT_MONSTER=1,STAT_MINDAMAGE=0,STAT_MAXDAMAGE=1,STAT_HITPOINTS=2;
struct D2GameStrc{};struct D2UnitStrc{int dwClassId=77,dwUnitType=1,pSeed=0;};
struct D2MissilesTxt{int dwHitPar[3]={1,3,0};int wProgOverlay=23;};
struct D2SkillsTxt{int dwCalc[2]={0,1};};
D2MissilesTxt missileRow;D2SkillsTxt skillRow;D2UnitStrc owner;
bool rowPresent=true,ownerPresent=true,skillPresent=true,pet=false,ally=false,demon=false,undead=false,upper=false;
int minimum=2,maximum=12,life=1000,maxLife=10000,fallback=512,draws=0,bound=-1,formulaCalls=0,seenRank=0,seenSkill=0,overlays=0,writes=0,petQueries=0,allyQueries=0,fallbackCalls=0;
D2MissilesTxt* SKILLS_GetMissilesTxtRecord(int){return rowPresent?&missileRow:nullptr;}
int MISSILE_GetSkill(D2UnitStrc*){return 101;}int MISSILE_GetLevel(D2UnitStrc*){return 7;}
D2UnitStrc* SUNIT_GetOwner(D2GameStrc*,D2UnitStrc*){return ownerPresent?&owner:nullptr;}
bool SUNIT_CanPetBeTargetedBySkill(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,int){++petQueries;return pet;}
bool SUNIT_CanAllyBeTargetedBySkill(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,int){++allyQueries;return ally;}
bool MONSTERS_IsDemon(D2UnitStrc*){return demon;}bool MONSTERS_IsUndead(D2UnitStrc*){return undead;}
D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int){return skillPresent?&skillRow:nullptr;}
int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int index,int skill,int rank){++formulaCalls;seenRank=rank;seenSkill=skill;return index?maximum:minimum;}
int ITEMS_RollLimitedRandomNumber(int*,int range){++draws;bound=range;return upper&&range>0?range-1:0;}
int MISSMODE_RollDamageValue(D2UnitStrc*,int,int,int){++fallbackCalls;return fallback;}
int STATLIST_GetMaxLifeFromUnit(D2UnitStrc*){return maxLife;}
int STATLIST_UnitGetStatValue(D2UnitStrc*,int,int){return life;}
void STATLIST_SetUnitStat(D2UnitStrc*,int,int value,int){life=value;++writes;}
void UNITS_SetOverlay(D2UnitStrc*,int,int){++overlays;}
void reset(){rowPresent=ownerPresent=skillPresent=true;pet=ally=demon=undead=upper=false;minimum=2;maximum=12;life=1000;maxLife=10000;draws=formulaCalls=seenRank=seenSkill=overlays=writes=petQueries=allyQueries=fallbackCalls=0;bound=-1;missileRow={};}
'''
 harness=r'''
int main(){D2GameStrc game;D2UnitStrc missile,target;bool first=true;printf("{\"eligibility\":[");
for(int heal=0;heal<2;++heal)for(int mode=0;mode<4;++mode)for(int own=0;own<2;++own)for(int p=0;p<2;++p)for(int a=0;a<2;++a)for(int kind:{0,1,4})for(int d=0;d<2;++d)for(int u=0;u<2;++u){
 reset();missileRow.dwHitPar[0]=heal;missileRow.dwHitPar[1]=mode;ownerPresent=own;pet=p;ally=a;target.dwUnitType=kind;demon=d;undead=u;
 int result=MISSMODE_SrvHit07_HolyBolt_FistOfTheHeavenBolt(&game,&missile,&target);
 printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",heal,mode,own,p,a,kind,d,u,result,life,writes,draws,petQueries,allyQueries);first=false;}
printf("],\"healing\":[");first=true;
for(int top=0;top<2;++top)for(int cap:{1200,10000})for(int overlay:{0,23})for(int zero=0;zero<2;++zero){reset();pet=true;upper=top;maxLife=cap;missileRow.wProgOverlay=overlay;if(zero){minimum=maximum=0;}
 int result=MISSMODE_SrvHit07_HolyBolt_FistOfTheHeavenBolt(&game,&missile,&target);
 printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",top,cap,overlay,zero,result,life,draws,bound,formulaCalls,seenRank,seenSkill,overlays,fallbackCalls);first=false;}
printf("],\"guards\":[");for(int guard=0;guard<4;++guard){reset();pet=true;if(guard==1)rowPresent=false;if(guard==3)skillPresent=false;
 int result=MISSMODE_SrvHit07_HolyBolt_FistOfTheHeavenBolt(&game,guard==0?nullptr:&missile,guard==2?nullptr:&target);
 printf("%s[%d,%d,%d,%d,%d]",guard?",":"",guard,result,writes,draws,overlays);}
printf("]}\n");}
'''
 STAGE.mkdir(parents=True,exist_ok=True);cpp=STAGE/'holy-bolt-hit.cpp';binary=STAGE/'holy-bolt-hit';cpp.write_text(scaffold+'\n'+routine+'\n'+harness)
 subprocess.run(['g++','-std=c++17','-O2','-Wall','-Wextra','-Wno-parentheses',str(cpp),'-o',str(binary)],check=True)
 result=json.loads(subprocess.check_output([str(binary)]))
 assert len(result['eligibility'])==768
 for heal,mode,own,pet,ally,kind,demon,undead,ret,life,writes,draws,pq,aq in result['eligibility']:
  healing=bool(heal and own and (pet or ally));expected=1 if healing else 3 if kind in (0,1) and (not mode or kind==1 and (demon if mode==2 else undead)) else 4
  assert (ret,life,writes,draws)==(expected,1512 if healing else 1000,int(healing),int(healing))
  assert pq==int(bool(heal and own)) and aq==int(bool(heal and own and not pet))
 for top,cap,overlay,zero,ret,life,draws,bound,calls,rank,skill,shown,fall in result['healing']:
  amount=512 if zero else 3071 if top else 512
  assert (ret,life,draws,bound,calls,rank,skill,shown,fall)==(1,min(1000+amount,cap),1,0 if zero else 2560,2,7,101,int(overlay>0),zero)
 assert result['guards']==[[i,1,0,0,0] for i in range(4)]
 (STAGE/'cases.json').write_text(json.dumps(result,indent=2)+'\n')
 record=dict(referenceVersion='3.3.93847',sourceCommit=COMMIT,sourcePath=UPSTREAM,sourceSha256=hashlib.sha256(blob).hexdigest(),completeFunction=name,counts={k:len(v) for k,v in result.items()},declaredServices=['Target eligibility/species/owner/table access are controlled callback returns,not executed engine services.','Formula slots return declared whole values;stored missile rank7/skill101 are observed. This does not execute current Prayer formulas.','Limited RNG returns lower/exclusive upper endpoints,not an authentic seed stream;stat/overlay stores are recorded services.'],limits=['Legacy return1/heal and return3/4species branches are not modern piercing/demon policy. Current DmgFunc15 is not supplied by this source.','No claim of modern executable,world collision,allegiance/PvP/owner,event,damage-return or client parity.'],artifacts={str(p.relative_to(ROOT)):hashlib.sha256(p.read_bytes()).hexdigest() for p in (Path(__file__).resolve(),SOURCE,cpp,binary,STAGE/'cases.json')})
 output=ROOT/'tools/reference/holy-bolt-hit-engine-evidence.json';output.write_text(json.dumps(record,indent=2)+'\n')
 print('Holy Bolt unchanged legacy hit callback:768eligibility/16healing/4guard cases;modern piercing and DmgFunc15 remain separate.')
if __name__=='__main__':main()
