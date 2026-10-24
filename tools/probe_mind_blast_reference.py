#!/usr/bin/env python3
"""Compile complete historical Mind Blast dispatch, callbacks and chance helper.

Current conversion-success damage is independently user-observed and deliberately
not patched into the historical routines being compared here.
"""
import argparse,hashlib,json,re,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'
def build(check=False):
 tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416';hashes={};blocks={}
 def source(path):
  raw=(STAGE/'d2moo-pinned-source'/path).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==path);hashes[path]=hashlib.sha256(raw).hexdigest();return raw.decode()
 def routine(text,name):
  m=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',text,re.M);assert m,name;i=text.index('{',m.start())+1;depth=1
  while depth:depth+=(text[i]=='{')-(text[i]=='}');i+=1
  blocks[name]=text[m.start():i];return blocks[name]+'\n'
 ass=source('source/D2Game/src/SKILLS/SkillAss.cpp');common=source('source/D2Common/src/D2Skills.cpp');unique=source('source/D2Game/src/MONSTER/MonsterUnique.cpp')
 cpp=r'''#include <cstdint>
#include <cstdio>
#include <cstring>
#include <algorithm>
#include <initializer_list>
#define __fastcall
#define __stdcall
#define D2_MAYBE_UNUSED(x) (void)(x)
constexpr int UNIT_MONSTER=1,UNIT_PLAYER=0,UNIT_ALIGNMENT_EVIL=0,AISPECIALSTATE_TERROR=2,STATE_CONVERSION=1,STATE_CONVERSION_SAVE=2,STATE_FREEZE=4,EVENTTYPE_REMOVESTATE=1;
constexpr int STAT_CONVERSION_LEVEL=0,STAT_CONVERSION_MAXHP=1,STAT_LEVEL=2,STAT_HITPOINTS=3,STAT_MAXHP=4;
struct D2ItemDataStrc{};
struct D2UnitStrc{int dwUnitType=1,dwUnitId=7,dwNodeIndex=3,pSeed=0,level=30,hp=1000*256,maxhp=1000*256;} owner,target;
struct D2GameStrc{int dwGameFrame=1000;void* pMemoryPool=nullptr;} game;
struct D2DamageStrc{int dwPhysDamage=0,dwStunLen=0;uint32_t wResultFlags=0,dwHitFlags=0;};
struct D2SkillsTxt{int dwParam[12]={0,0,150,100,15,40};int dwAuraRangeCalc=1,dwAuraFilter=33667;uint32_t wResultFlags=9,dwHitFlags=0;} row;
struct D2StatListStrc{bool present=false;int state=0,flags=0,expiry=0,source=0,savedLevel=0,savedMax=0;void(*remove)(D2UnitStrc*,int,D2StatListStrc*)=nullptr;} conversion,saved,auras[2];
struct D2SrvDo51Strc{D2DamageStrc* pDamage;int nMonCurseResSubstraction=0,nParam1=0,nParam2=0;};
struct D2AuraCallbackStrc{D2GameStrc* pGame;D2UnitStrc* pOwner;void* pArgs;};
int scenario=0,draw=0,duration=0,draws=0,alignment=0,removed=0,events=0,damageCalls=0,physCalls=0,elemCalls=0,searches=0,rangeOut=0,filterOut=0,progressive=0;
bool presentRow=true,coordinates=true;
D2SkillsTxt* DATATBLS_GetSkillsTxtRecord(int){return presentRow?&row:nullptr;}
D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int id){return DATATBLS_GetSkillsTxtRecord(id);}
int MONSTERS_GetHirelingTypeId(D2UnitStrc*){return scenario==2;}
int STATLIST_GetUnitAlignment(D2UnitStrc*){return scenario==3?1:alignment;}
bool sub_6FD15190(D2UnitStrc*,int){return scenario!=4;}
int ITEMS_RollLimitedRandomNumber(int*,int max){int value=draws++==0?draw:duration;return max>0?std::clamp(value,0,max-1):0;}
D2StatListStrc* STATLIST_GetStatListFromUnitAndState(D2UnitStrc*,int state){auto* p=state==STATE_CONVERSION?&conversion:&saved;return p->present?p:nullptr;}
D2StatListStrc* STATLIST_AllocStatList(void*,int flags,int expiry,int,int source){if(flags==0x802 && scenario==5 || flags==0 && scenario==8)return nullptr;auto* p=flags==0x802?&conversion:&saved;*p={true,0,flags,expiry,source};return p;}
void STATLIST_SetState(D2StatListStrc* p,int state){p->state=state;}
void STATLIST_SetStatRemoveCallback(D2StatListStrc* p,void(*f)(D2UnitStrc*,int,D2StatListStrc*)){p->remove=f;}
void D2COMMON_10475_PostStatToStatList(D2UnitStrc*,D2StatListStrc*,int){}
void STATES_ToggleState(D2UnitStrc*,int,int){}
void D2COMMON_10476(D2StatListStrc* p,int expiry){p->expiry=expiry;}
void EVENT_SetEvent(D2GameStrc*,D2UnitStrc*,int,int,int,int){}
void sub_6FD154D0(D2UnitStrc*){}
void sub_6FCBDD30(D2UnitStrc*,uint32_t value,int){alignment=value;}
void D2GAME_UpdateSummonAI_6FC401F0(D2GameStrc*,D2UnitStrc*,int,int){}
D2StatListStrc* STATLIST_GetStatListFromUnitAndFlag(D2UnitStrc*,int){for(auto& p:auras)if(p.present)return &p;return nullptr;}
void D2Common_10474(D2UnitStrc*,D2StatListStrc* p){p->present=false;}
void STATLIST_FreeStatList(D2StatListStrc* p){if(p->flags==8)removed++;p->present=false;}
void UNITS_SetTargetUnitForDynamicUnit(D2UnitStrc*,D2UnitStrc*){}
int STATLIST_UnitGetStatValue(D2UnitStrc* p,int stat,int){return stat==STAT_LEVEL?p->level:stat==STAT_HITPOINTS?p->hp:p->maxhp;}
int STATLIST_GetMaxLifeFromUnit(D2UnitStrc* p){return p->maxhp;}
int STATLIST_GetStatValue(D2StatListStrc* p,int stat,int){return stat==STAT_CONVERSION_LEVEL?p->savedLevel:p->savedMax;}
void STATLIST_SetStatIfListIsValid(D2StatListStrc* p,int stat,int value,int){if(stat==STAT_CONVERSION_LEVEL)p->savedLevel=value;else p->savedMax=value;}
void STATLIST_SetUnitStat(D2UnitStrc* p,int stat,int value,int){if(stat==STAT_LEVEL)p->level=value;else if(stat==STAT_HITPOINTS)p->hp=value;else p->maxhp=value;}
D2GameStrc* SUNIT_GetGameFromUnit(D2UnitStrc*){return &game;}
void D2GAME_TARGETS_Last_6FC40380(D2GameStrc*,D2UnitStrc*){}
void SUNITDMG_ExecuteEvents(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,int,D2DamageStrc*){events++;}
void SUNITDMG_ExecuteMissileDamage(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,D2DamageStrc*){damageCalls++;}
bool D2GAME_GetXAndYFromTargetUnit_6FD14020(D2GameStrc*,D2UnitStrc*,int* x,int* y){*x=15;*y=17;return coordinates;}
int SKILLS_EvaluateProgressiveSkillCalc(D2UnitStrc*,int,int){return progressive;}
int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int,int,int){return 4;}
void D2GAME_RollPhysicalDamage_6FD14EC0(D2UnitStrc*,D2DamageStrc* p,int,int){physCalls++;p->dwPhysDamage=2377;}
void D2GAME_RollElementalDamage_6FD14DD0(D2UnitStrc*,D2DamageStrc* p,int,int){elemCalls++;p->dwStunLen=70;}
void sub_6FD0FE80(D2GameStrc*,D2UnitStrc*,int,int,int range,int filter,int(*)(D2AuraCallbackStrc*,D2UnitStrc*),void*,int,const char*,int){searches++;rangeOut=range;filterOut=filter;}
void reset(){conversion={};saved={};for(auto& p:auras)p={true,0,8};draws=alignment=removed=events=damageCalls=physCalls=elemCalls=searches=rangeOut=filterOut=0;presentRow=coordinates=true;progressive=0;owner={};owner.dwUnitType=UNIT_PLAYER;owner.dwUnitId=11;target={};}
'''
 for text,name in [(common,'D2Common_11033'),(common,'D2COMMON_11036_GetMonCurseResistanceSubtraction'),(unique,'MONSTERUNIQUE_CalculatePercentage'),(ass,'SKILLS_StatRemoveCallback_MindBlast'),(ass,'SKILLS_AuraCallback_MindBlast'),(ass,'SKILLS_SrvDo051_MindBlast')]:cpp+=routine(text,name)
 cpp+=r'''int main(){bool first=true;printf("{\"chance\":[");
for(int exists:{0,1})for(int rank=-1;rank<=255;rank++){reset();presentRow=exists;int chance=D2COMMON_11036_GetMonCurseResistanceSubtraction(rank,273);printf("%s[%d,%d,%d]",first?"":",",exists,rank,chance);first=false;}
printf("],\"callback\":[");first=true;
for(int s=0;s<9;s++)for(int pair=0;pair<4;pair++)for(int percent:{25,100})for(int chance:{0,18,36})for(int roll:{0,18,19,36,37,99})for(int length:{0,99}){
 reset();scenario=s;owner.level=pair==0?24:pair==1?30:99;target.level=pair==0?80:pair==1?30:pair==2?80:0;target.hp=1000*percent*256/100;if(s==1)target.dwUnitType=UNIT_PLAYER;
 if(s==6)conversion={true,STATE_CONVERSION,0x802,123,22};if(s==7)saved={true,STATE_CONVERSION_SAVE,0,0,7,80,4000};
 int originalLevel=target.level;draw=roll;duration=length;D2DamageStrc damage;D2SrvDo51Strc args{&damage,chance,150,100};D2AuraCallbackStrc context{&game,&owner,&args};SKILLS_AuraCallback_MindBlast(&context,&target);
 printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",s,owner.level,originalLevel,percent,chance,roll,length,alignment==2,conversion.expiry,target.level,target.hp,target.maxhp,events,damageCalls,draws,removed,saved.present,saved.savedLevel,saved.savedMax,conversion.source,alignment);first=false;
}
printf("],\"expiry\":[");first=true;
for(int exists:{0,1})for(int maxhp:{0,1,20,100})for(int hp:{0,1,10,75,100}){reset();saved={bool(exists),STATE_CONVERSION_SAVE,0,0,7,80,4000};target.level=30;target.maxhp=maxhp*256;target.hp=hp*256;SKILLS_StatRemoveCallback_MindBlast(&target,STATE_CONVERSION,&conversion);printf("%s[%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",exists,maxhp,hp,target.level,target.hp,target.maxhp,alignment,removed);first=false;}
printf("],\"dispatch\":[");first=true;
for(int exists:{0,1})for(int coords:{0,1})for(int range:{0,7})for(int rank:{1,21}){reset();presentRow=exists;coordinates=coords;progressive=range;int result=SKILLS_SrvDo051_MindBlast(&game,&owner,273,rank);printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",exists,coords,range,rank,result,physCalls,elemCalls,searches,rangeOut,filterOut);first=false;}
printf("]}\n");}'''
 path=STAGE/'mind-blast-source-probe.cpp';path.write_text(cpp);binary=STAGE/'mind-blast-source-probe';subprocess.run(['g++','-std=c++20','-O2',str(path),'-o',str(binary)],check=True)
 data=json.loads(subprocess.check_output([str(binary)]));assert {k:len(v) for k,v in data.items()}==dict(chance=514,callback=2592,expiry=40,dispatch=16)
 for exists,rank,value in data['chance']:assert value==(15+25*min(100,110*rank//(rank+6))//100 if exists and rank>0 else 0)
 for s,level,original,percent,chance,roll,length,success,expires,newlevel,hp,maxhp,events,damage,draws,removed,hasSave,savelevel,savemax,sourceid,alignment in data['callback']:
  eligible=s not in (1,2,3,4);converted=eligible and roll<=chance and s!=5
  scaled=converted and original>level and s not in (7,8)
  assert bool(success)==converted and (events,damage,draws,removed)==(int(not converted),int(not converted),int(eligible)+int(eligible and roll<=chance),2 if converted else 0)
  assert expires==(1150+length if converted else 123 if s==6 else 0)
  assert newlevel==(level if scaled else original) and hp==((1000*percent//100)*level//original*256 if scaled else 1000*percent*256//100) and maxhp==(1000*level//original*256 if scaled else 1000*256)
  assert bool(hasSave)==(s==7 or scaled)
  if scaled:assert (savelevel,savemax)==(original,1000)
  assert sourceid==(22 if s==6 else 11 if converted else 0) and alignment==(2 if converted else 0)
 for exists,maximum,hp,level,output,outmax,alignment,removed in data['expiry']:
  restored=min(maximum,max(1,4000*hp//maximum) if maximum else 1)*256
  assert (level,output,outmax,alignment,removed)==((80,restored,4000*256,0,2) if exists else (30,hp*256,maximum*256,0,2))
 for exists,coords,progressive,rank,result,physical,elemental,searches,range_,filter_ in data['dispatch']:
  accepted=bool(exists and coords);assert (result,physical,elemental,searches,range_,filter_)==(int(accepted),int(accepted),int(accepted),int(accepted),(progressive or 4) if accepted else 0,33667 if accepted else 0)
 fixture=STAGE/'mind-blast-source-probe.json';fixture.write_text(json.dumps(data,separators=(',',':'))+'\n')
 evidence=dict(referenceVersion='3.3.93847',talentId='assassin.mindblast',proofEnablesHandler=False,legacyCommit=tree['sha'],sourceHashes=hashes,compiledUnchangedRoutines={k:hashlib.sha256(v.encode()).hexdigest() for k,v in blocks.items()},fixture=dict(path=str(fixture.relative_to(ROOT)),sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(),caseCounts={k:len(v) for k,v in data.items()}),currentObservation='tools/reference/mind-blast-current-observations.json',established=['The complete historical callback uses an inclusive conversion roll,150+limited100frames, shared existing conversion-state refresh, aura-flag8 cleanup and optional higher-level whole-life/level scaling. Its successful path performs no damage/events, explicitly conflicting with current user damage observations.','The complete removal callback restores original level/max life in FP256 but clamps current life using the current scaled maximum and reconstructed ratio. This unusual clamp is retained literally; it is not replaced with percentage-preserving healing.','Complete do51 rolls physical/elemental data once before area search; search receives the existing packet. Chance helper reads Param5/6 through the full diminishing helper. Current Param8=0 adds no Psychic Hammer hard-rank synergy.'],services=['Stat/list allocation, existing-state presence, flags/ownership, alignment/AI/targets, table/formula, two supplied limited draws and event/damage calls are explicit services. Existing conversion and conversion-save cases with evil alignment are synthetic branch comparisons, not ordinary recast evidence.','Physical/elemental roll services write declared sentinels; full skill damage getters and current native damage/mitigation are separate. Area search is a recorded service without room/grid/height/filter simulation. State factory allocation-failure cases and owner/node updates do not simulate the complete actor/event engine.'],limits=['Historical source preparation does not enable learned Mind Blast. Current successful conversion must damage without knockback or stun, superseding the old no-damage branch. Exact modern damage/allegiance/level ordering and unusual higher-level restoration require explicit acceptance limits.','The matrix uses ordinary whole HP and positive caster levels24/30/99, with one zero target-level branch. Full fractional/tiny/overflow, species/AI, event cleanup, NPC/PvP/foreign/item-only suppliers and complete native/graphical/persistence acceptance are not established.'])
 target=ROOT/'tools/reference/mind-blast-engine-evidence.json';rendered=json.dumps(evidence,indent=2)+'\n'
 if check:assert target.read_text()==rendered
 else:target.write_text(rendered)
 print('Mind Blast:514 chance,2592 complete callback,40 removal and16 dispatch source cases pass; current success-damage conflict remains explicit.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
