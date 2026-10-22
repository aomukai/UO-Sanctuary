#!/usr/bin/env python3
"""Compile bounded Howl launch/hit/Terror eligibility, curse gates and AI dispatch."""
from pathlib import Path
import hashlib,json,re,subprocess
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills';BASE=STAGE/'d2moo-pinned-source'
tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
blobs={x['path']:x['sha'] for x in tree['tree'] if x['type']=='blob'};hashes={}
def source(path):
 raw=(BASE/path).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==blobs[path],path
 hashes[path]=hashlib.sha256(raw).hexdigest();return raw.decode()
def routine(text,name):
 m=re.search(r'^.*\b'+re.escape(name)+r'\([^;\n]*\)\s*\n\{',text,re.M);assert m,name
 end=text.index('{',m.start())+1;depth=1
 while depth:depth+=(text[end]=='{')-(text[end]=='}');end+=1
 return text[m.start():end]
sor=source('source/D2Game/src/SKILLS/SkillSor.cpp');miss=source('source/D2Game/src/MISSILES/MissMode.cpp');util=source('source/D2Game/src/AI/AiUtil.cpp');think=source('source/D2Game/src/AI/AiThink.cpp');units=source('source/D2Common/src/Units/Units.cpp');skills=source('source/D2Game/src/SKILLS/Skills.cpp');enums=source('source/D2Game/include/AI/AiStates.h')
assert 'AISPECIALSTATE_TERROR = 11' in enums
blocks={name:routine(text,name) for text,names in [(sor,['SKILLS_SrvDo022_NovaAttack']),(miss,['MISSMODE_SrvHit17_Howl']),(util,['AIUTIL_CanUnitSwitchAi','AIUTIL_ApplyTerrorCurseState']),(think,['AITHINK_CanUnitSwitchAi','D2GAME_AI_SpecialState11_6FCE7E80']),(units,['UNITS_CanSwitchAI'])] for name in names}
tactics=source('source/D2Game/src/AI/AiTactics.cpp');monsterai=source('source/D2Game/src/MONSTER/MonsterAI.cpp')
curse=routine(skills,'sub_6FD10EC0');gate=curse[curse.index('{')+1:curse.index('    uint32_t nStatListFlags')]
initialize=routine(think,'AITHINK_ExecuteAiFn');assert 'pAiControl->dwAiParam[0] = 0;' in initialize
cpp=r'''#include <algorithm>
#include <cstdlib>
#include <cstdint>
#include <cstdio>
#include <initializer_list>
#define __fastcall
using BOOL=int;using D2C_AiSpecialState=int;
constexpr int TRUE=1,FALSE=0,UNIT_PLAYER=0,UNIT_MONSTER=1,UNITFLAG_SKSRVDOFUNC=4,UNITFLAG_CANBEATTACKED=1;
constexpr int AISPECIALSTATE_NONE=0,AISPECIALSTATE_DIMVISION=10,AISPECIALSTATE_TERROR=11,AISPECIALSTATE_TAUNT=12,AISPECIALSTATE_INVALID=19;
constexpr int STATE_UNINTERRUPTABLE=2,STATE_ATTRACT=57,STATE_TERROR=56,SKILL_TERROR=77,STAT_LEVEL=0,STAT_CURSE_RESISTANCE=1;
constexpr int MONTYPEFLAG_UNIQUE=1,MONTYPEFLAG_SUPERUNIQUE=2,MONSTATSFLAG_BOSS=1,MONSTATSFLAG_SWITCHAI=2,MONSTATS2MODEFLAG_WL=1;
constexpr int MONSTATSFLAGINDEX_NPC=2,MONSTATSFLAGINDEX_INTERACT=3,MONSTATS2FLAGINDEX_ISATT=0,MONMODE_ATTACK1=1,MONMODE_RUN=2;
constexpr uint32_t gdwBitMasks[]={1,2,4,8,16};
struct D2AiControlStrc{int dwAiParam[3]={};};
struct D2MonStatsTxt{int wMonStatsEx=1;uint32_t dwMonStatsFlags=2;int nVelocity=8,nRun=12;};
struct D2MonStats2Txt{uint32_t dwModeFlags=7,dwFlags=1;};
struct MonsterData{D2AiControlStrc* pAiControl=nullptr;};
struct D2UnitStrc{int dwUnitType=1,dwClassId=1,dwFlags=1,level=30,skill=130,rank=1,monType=0,curseResist=0,x=0,y=0,size=2;bool terror=false,attract=false,dead=false,locked=false;MonsterData* pMonsterData=nullptr;D2UnitStrc* owner=nullptr;D2UnitStrc* target=nullptr;};
struct D2GameStrc{} game;struct D2StatListStrc{} statlist;struct D2StatesTxt{} stateRow;
struct D2SkillsTxt{int wAuraTargetState=56,dwParam[6]={12,2,24,5,75,25},dwCalc[1]={0};} row;
struct D2CurseStrc{D2UnitStrc* pUnit=nullptr;D2UnitStrc* pTarget=nullptr;int nSkillLevel=0,nDuration=0,nStat=0,nSkill=0,nState=0;};
struct D2AiTickParamStrc{D2AiControlStrc* pAiControl=nullptr;D2MonStatsTxt* pMonstatsTxt=nullptr;D2UnitStrc* pTarget=nullptr;int nTargetDistance=0;bool bCombat=false;};
struct Tables{int nStatesTxtRecordCount=256,nMissilesTxtRecordCount=300;} tables;Tables* sgptDataTables=&tables;
D2MonStatsTxt monster;D2MonStats2Txt modes;bool hasRow=true,hasMon=true,hasModes=true,hasState=true,curseMask=true;
int missileId=148,launched=0,launchVelocity=0,curseCalls=0,appliedFrames=-1,appliedSkill=-1,appliedRank=-1,aiCalls=0,idle=0,neutral=0,attack=0,escape=0,escapeFlag=-1,escapeDistance=0,fallback=0,velocity=-1,deletedEvents=0,clearedTarget=0;bool escapeResult=true;
int CLIENTS_GetUnitX(D2UnitStrc* p){return p->x;}
int CLIENTS_GetUnitY(D2UnitStrc* p){return p->y;}
int UNITS_GetUnitSizeX(D2UnitStrc* p){return p->size;}
D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int){return hasRow?&row:nullptr;}
D2MonStatsTxt* DATATBLS_GetMonStatsTxtRecord(int id){return hasMon&&id>=0?&monster:nullptr;}
D2MonStatsTxt* MONSTERMODE_GetMonStatsTxtRecord(int id){return DATATBLS_GetMonStatsTxtRecord(id);}
D2MonStats2Txt* UNITS_GetMonStats2TxtRecord(int){return hasModes?&modes:nullptr;}
D2MonStats2Txt* MONSTERREGION_GetMonStats2TxtRecord(int){return hasModes?&modes:nullptr;}
D2GameStrc* SUNIT_GetGameFromUnit(D2UnitStrc*){return &game;}
D2StatesTxt* SKILLS_GetStatesTxtRecord(int){return hasState?&stateRow:nullptr;}
int STATES_CheckStateMaskCurseByStateId(int){return curseMask;}
int MONSTERUNIQUE_CalculatePercentage(int value,int percent,int divisor){return int(int64_t(value)*percent/divisor);}
int STATES_CheckState(D2UnitStrc* p,int state){return p&&(state==STATE_TERROR?p->terror:state==STATE_ATTRACT?p->attract:p->locked);}
int STATLIST_UnitGetStatValue(D2UnitStrc* p,int stat,int){return stat==STAT_LEVEL?p->level:p->curseResist;}
int SUNIT_IsDead(D2UnitStrc* p){return !p||p->dead;}
int MONSTERUNIQUE_CheckMonTypeFlag(D2UnitStrc* p,int flags){return (p->monType&flags)!=0;}
int MISSILE_GetSkill(D2UnitStrc* p){return p->skill;}
int MISSILE_GetLevel(D2UnitStrc* p){return p->rank;}
D2UnitStrc* SUNIT_GetOwner(D2GameStrc*,D2UnitStrc* p){return p->owner;}
int SKILLS_GetProgressiveSkillMissileId(D2UnitStrc*,int){return missileId;}
int DATATBLS_GetMissileVelocityFromMissilesTxt(int,int){return 12;}
int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int,int,int rank){return (rank-1)*2;}
void sub_6FD14170(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,int,int,int,int speed){launched=64;launchVelocity=speed;}
void UNITS_SetTargetUnitForDynamicUnit(D2UnitStrc* p,D2UnitStrc* target){p->target=target;}
void AITHINK_ExecuteAiFn(D2GameStrc*,D2UnitStrc*,D2AiControlStrc*,int){aiCalls++;}
void AITACTICS_Idle(D2GameStrc*,D2UnitStrc*,int frames){idle=frames;}
void AITACTICS_IdleInNeutralMode(D2GameStrc*,D2UnitStrc*,int frames){neutral=frames;}
void AITACTICS_SetVelocity(D2UnitStrc*,int,int percent,int){velocity=percent;}
void AITACTICS_ChangeModeAndTargetUnit(D2GameStrc*,D2UnitStrc*,int,D2UnitStrc*){attack++;}
bool sub_6FCD06D0(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,int distance,int flag){escape=2;escapeFlag=flag;escapeDistance=distance;return escapeResult;}
bool D2GAME_AICORE_Escape_6FCD0560(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,int distance,int flag){escape=1;escapeFlag=flag;escapeDistance=distance;return escapeResult;}
void AITACTICS_WalkCloseToUnit(D2GameStrc*,D2UnitStrc*,unsigned){fallback++;}
void sub_6FC61F00(D2UnitStrc*){clearedTarget++;}
constexpr int EVENTTYPE_AITHINK=1;
void D2GAME_EVENTS_Delete_6FC34840(D2GameStrc*,D2UnitStrc*,int,int){deletedEvents++;}
void Reset(){launched=launchVelocity=curseCalls=aiCalls=idle=neutral=attack=escape=fallback=deletedEvents=clearedTarget=0;appliedFrames=appliedSkill=appliedRank=escapeFlag=velocity=-1;escapeDistance=0;}
'''
cpp+='D2StatListStrc* CurseGate(D2CurseStrc* pCurse){'+gate+'return &statlist;}\n'
cpp+=r'''D2StatListStrc* sub_6FD10EC0(D2CurseStrc* p){curseCalls++;appliedFrames=p->nDuration;appliedSkill=p->nSkill;appliedRank=p->nSkillLevel;auto* result=CurseGate(p);appliedFrames=p->nDuration;if(result)p->pTarget->terror=true;return result;}
'''
for name in ['AIUTIL_GetDistanceToCoordinatesWithSize','AIUTIL_GetDistanceToCoordinates_FullUnitSize','AIUTIL_GetDistanceToCoordinates_NoUnitSize']:blocks[name]=routine(util,name);cpp+=blocks[name]+'\n'
for name in ['UNITS_CanSwitchAI','AITHINK_CanUnitSwitchAi','AIUTIL_CanUnitSwitchAi','AIUTIL_ApplyTerrorCurseState','MISSMODE_SrvHit17_Howl','SKILLS_SrvDo022_NovaAttack','D2GAME_AI_SpecialState11_6FCE7E80']:cpp+=blocks[name]+'\n'
cpp+=r'''int main(){
D2AiControlStrc control;MonsterData data{&control};D2UnitStrc owner;owner.dwUnitType=0;owner.level=30;
std::printf("{\"launchCases\":[");bool first=true;
for(int rank:{1,20,21})for(int valid:{0,1})for(int mid:{-1,148,300}){Reset();hasRow=valid;missileId=mid;int result=SKILLS_SrvDo022_NovaAttack(&game,&owner,130,rank);if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d]",rank,valid,mid,result,launched,launchVelocity);}
std::printf("],\"hitCases\":[");first=true;hasRow=true;missileId=148;
for(int rank:{0,1,20,21})for(int level:{30,32,33,51,52,53})for(int feared:{0,1})for(int ownerPresent:{0,1}){
 Reset();D2UnitStrc target;target.level=level;target.terror=feared;target.pMonsterData=&data;D2UnitStrc missile;missile.rank=rank;missile.owner=ownerPresent?&owner:nullptr;int result=MISSMODE_SrvHit17_Howl(&game,&missile,&target);
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",rank,level,feared,ownerPresent,result,curseCalls,appliedFrames,appliedSkill,appliedRank,aiCalls);
}
std::printf("],\"eligibilityCases\":[");first=true;
for(int flags:{0,1,2,3})for(int walk:{0,1})for(int unique:{0,1,2,3})for(int locked:{0,1})for(int dead:{0,1}){
 D2UnitStrc target;target.monType=unique;target.locked=locked;target.dead=dead;monster.dwMonStatsFlags=flags;modes.dwModeFlags=walk;int result=AIUTIL_CanUnitSwitchAi(&target,AISPECIALSTATE_TERROR);
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d]",flags,walk,unique,locked,dead,result);
}
std::printf("],\"curseGateCases\":[");first=true;monster.dwMonStatsFlags=2;modes.dwModeFlags=7;
for(int resist:{-50,0,50,99,100})for(int attract:{0,1})for(int npc:{0,1})for(int attackable:{0,1}){
 D2UnitStrc target;target.curseResist=resist;target.attract=attract;monster.dwMonStatsFlags=2|(npc?4:0);modes.dwFlags=attackable?1:0;D2CurseStrc curse;curse.pTarget=&target;curse.nState=56;curse.nDuration=75;int ok=CurseGate(&curse)!=nullptr;
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d]",resist,attract,npc,attackable,ok,curse.nDuration);
}
std::printf("],\"helperCases\":[");first=true;monster.dwMonStatsFlags=2;modes.dwFlags=1;modes.dwModeFlags=7;
for(int dead:{0,1})for(int attract:{0,1})for(int resist:{-50,0,99,100})for(int distance:{24,119}){
 Reset();D2UnitStrc target;target.dead=dead;target.attract=attract;target.curseResist=resist;target.pMonsterData=&data;AIUTIL_ApplyTerrorCurseState(&game,&owner,&target,130,distance,75);
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",dead,attract,resist,distance,curseCalls,appliedFrames,aiCalls,idle,target.terror,target.target==&owner);
}
std::printf("],\"hitGuardCases\":[");first=true;
for(int valid:{0,1})for(int own:{0,1})for(int targetType:{-1,0,1})for(int state:{-1,56,256}){
 Reset();hasRow=valid;row.wAuraTargetState=state;D2UnitStrc target;target.dwUnitType=targetType;target.pMonsterData=&data;D2UnitStrc missile;missile.owner=own?&owner:nullptr;int result=MISSMODE_SrvHit17_Howl(&game,&missile,targetType<0?nullptr:&target);
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d]",valid,own,targetType,state,result,curseCalls);
}
hasRow=true;row.wAuraTargetState=56;
std::printf("],\"terrorAiCases\":[");first=true;monster.dwMonStatsFlags=2;modes.dwFlags=1;
for(int active:{0,1})for(int started:{0,1})for(int distance:{1,30,31})for(int combat:{0,1})for(int run:{0,1})for(int success:{0,1})for(int runSpeed:{0,8,12,32}){
 Reset();escapeResult=success;D2UnitStrc target;target.terror=active;control.dwAiParam[0]=0;control.dwAiParam[2]=started;monster.nVelocity=8;monster.nRun=runSpeed;modes.dwModeFlags=3|(run?4:0);
 D2AiTickParamStrc tick{&control,&monster,&owner,distance,bool(combat)};D2GAME_AI_SpecialState11_6FCE7E80(&game,&target,&tick);
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",active,started,distance,combat,run,success,runSpeed,aiCalls,idle,neutral,attack,escape,escapeFlag,escapeDistance,fallback,velocity,control.dwAiParam[2],deletedEvents);
}
std::printf("],\"distanceCases\":[");first=true;
for(int dx:{-35,-2,-1,0,1,2,35})for(int dy:{-35,-2,-1,0,1,2,35})for(int size:{1,2,3}){
 D2UnitStrc target;target.x=dx;target.y=dy;target.size=size;D2UnitStrc source;int full=AIUTIL_GetDistanceToCoordinates_FullUnitSize(&target,&source),plain=AIUTIL_GetDistanceToCoordinates_NoUnitSize(&target,0,0);
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d]",dx,dy,size,full,plain);
}std::printf("]}\n");}
'''
path=STAGE/'howl-source-probe.cpp';path.write_text(cpp);binary=STAGE/'howl-source-probe';subprocess.run(['g++','-std=c++20','-O2',str(path),'-o',str(binary)],check=True)
data=json.loads(subprocess.check_output([str(binary)]))
for rank,valid,mid,result,count,velocity in data['launchCases']:
 good=bool(valid and 0<=mid<300);assert (result,count,velocity)==(int(good),64 if good else 0,12+2*(rank-1) if good else 0)
for rank,level,feared,owner,result,calls,frames,skill,stored,ai in data['hitCases']:
 apply=bool(owner and not feared and rank+2+30>level)
 assert result==int(not owner) and calls==ai==int(apply)
 assert (frames,skill,stored)==((75+25*(rank-1) if rank>0 else 0,77,1) if apply else (-1,-1,-1))
for flags,walk,unique,locked,dead,result in data['eligibilityCases']:
 # This eligibility routine accepts a dead attackable unit; the caller supplies
 # its own explicit SUNIT_IsDead rejection. Keep both contracts distinct.
 assert result==int(bool(flags&2) and not(flags&1) and bool(walk) and not unique and not locked)
for resist,attract,npc,attackable,ok,duration in data['curseGateCases']:
 expected=not npc and attackable and resist<100 and not attract
 assert ok==int(bool(expected))
 adjusted=75-int(75*resist/100) if not npc and attackable and resist<100 else 75
 assert duration==adjusted
for dead,attract,resist,distance,calls,duration,ai,idle,terror,target in data['helperCases']:
 good=not dead;expectedDuration=75-int(75*resist/100) if resist<100 else 75
 assert (calls,duration,ai,idle,terror,target)==(int(good),expectedDuration if good else -1,int(good),int(good),int(good and not attract and resist<100),int(good))
for valid,owner,targetType,state,result,calls in data['hitGuardCases']:
 expected=1 if not valid or not owner else 0 if targetType!=1 else int(state not in range(256))
 assert result==expected and calls==int(bool(valid and owner and targetType==1 and state in range(256)))
for x in data['terrorAiCases']:
 active,started,distance,combat,run,success,runSpeed,ai,idle,neutral,attack,escape,flag,dist,fallback,velocity,endstarted,deleted=x
 if not active:assert (ai,idle,neutral,attack,escape,velocity,endstarted,deleted)==(1,1,0,0,0,-1,started,0)
 elif distance>30:assert (ai,idle,neutral,attack,escape,velocity,endstarted,deleted)==(0,0,10,0,0,-1,started,0)
 elif started and combat:assert (ai,idle,neutral,attack,escape,velocity,endstarted,deleted)==(0,0,0,1,0,-1,1,0)
 else:
  assert (ai,idle,neutral,attack,escape,flag,dist,fallback,velocity,endstarted,deleted)==(0,0,0,0,2 if run else 1,started,30,int(bool(started and not success)),max(0,min(120,100*runSpeed//8-100)),1,0 if started else 1)
for dx,dy,size,full,plain in data['distanceCases']:
 x,y=abs(abs(dx)-size),abs(abs(dy)-size);assert full==max(x,y)+min(x,y)//2 and plain==max(abs(dx),abs(dy))+min(abs(dx),abs(dy))//2
(STAGE/'howl-source-probe.json').write_text(json.dumps(data,indent=2)+'\n')
for name,text in [('terrorInitializeReview',routine(think,'D2GAME_AI_SpecialState11_6FCE7E20')),('targetOverrideWriteReview',routine(monsterai,'sub_6FC61E30')),('targetOverrideClearReview',routine(monsterai,'sub_6FC61F00')),('targetOverrideReadReview',routine(util,'sub_6FCF2920')),('targetSelectionReview',routine(util,'sub_6FCF2110')),('targetSelectionWrapperReview',routine(tactics,'sub_6FCCF9D0'))]:blocks[name]=text
blocks['curseLeadingGates']=gate;blocks['curseCompleteReview']=curse;blocks['aiInitializationReview']=initialize
out=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],sourceHashes=hashes,probe=dict(caseCounts={k:len(v) for k,v in data.items()},blockHashes={k:hashlib.sha256(v.encode()).hexdigest() for k,v in blocks.items()},dataPath='.local/class-skills/howl-source-probe.json'),established=[
'Howl uses common ring do22: calc1 plus missile table velocity, source64-vector factory. Effective rank1/20/21 launch velocities12/50/52 before factory scaling; pinned cost4mana.',
'Hit17 uses stored missile rank plus current owner level and Param2, strict > target level; already-Terror state is not refreshed. Owner/skill/state/type checks precede shared fear helper.',
'The AI helper explicitly rejects dead targets before its separate AI-switch eligibility routine, which itself can accept an attackable dead unit. Preserve caller/callee gates independently.',
'Helper stores Terror skill77 at rank1 with Howl-supplied duration, independently of owner class or learned Terror rank. Calculated distance is unused. No difficulty division occurs in this helper or the inspected complete generic curse function.',
'Generic curse leading gates check source NPC/attackability/state, curse resistance and Attract. Positive/negative curse resistance modifies duration before allocation;100+ rejects. Helper still sets dynamic target and switches/idles AI even when curse allocation fails.',
'Common Terror AI defaults its distance parameter to30, idles10frames only when target distance>30, derives capped0..120 run percentage, initially escapes and deletes pending AI event, can attack in later combat, and falls back to walking near when later escape fails.',
'Terror initialization records the caster target override; initial escape clears that override, allowing later common AI target selection. Do not make either fixed cast origin or permanent caster tracking the complete source behavior.',
'Target distances use source integer weighted-axis distance and the target full-size subtraction, not Euclidean center distance;147 compiled raw-coordinate cases cover this contract before native tile projection.',
'General AI initialization normally zeros all three parameters before special-state dispatch. Callback-bearing previous AI has an early transition branch; species-specific preservation needs separate handling.'],stubs=[
'Unchanged do22/hit17/AI eligibility/helper/Terror tick functions compile with declared table/stat/event/target/path/movement/AI-selection stubs. Shared ring factory geometry was compiled separately in shout-engine-evidence.json; this probe records its launch arguments.',
'Target override/selection/initialization functions are hash-checked reviews; source coordinate-distance helpers are additionally compiled. Full target acquisition/LOS/vision/player/monster scan remains outside this probe.',
'Generic curse source leading gate block is compiled; the complete allocation/replacement/event/statlist function and AI initializer are hashed/reviewed, not linked into a full engine.',
'Attackable state/skill rows, owner lookup, table flags, target distances/combat and escape success are supplied by fixture. Escape/path routines are recorded calls; no world movement/LOS/path selection is certified.',
'No complete monster target acquisition, native projected distance/collision metric, animation/velocity integration, source callback dispatch or modern executable capture is claimed.'],limits=[
'No Howl handler enabled by this evidence. Reuse current rays with per-skill motion and a shared fear consumer that can express source distance/idle/initial-escape/later-combat transitions.',
'Current native Necromancer Terror and NamedControl fear do not fully express this common AI: fixed origin or unconditional flee differs from the compiled dispatch. Avoid silently equating those existing behaviors with Howl.',
'Generic curse failure still switches AI transiently in supplementary source. Exact state/curse resistance/Attract and modern owner/death/item/client transitions remain acceptance work.',
'Native monster species mappings, champion/unique/uninterruptable/charged-item cases, missing caster/target selection, path fallbacks, exact SC/FCR/geometry/collision and modern duration/scheduling remain open.'])
(ROOT/'tools/reference/howl-engine-evidence.json').write_text(json.dumps(out,indent=2)+'\n');print('Howl bounded source probes passed:',out['probe']['caseCounts'])
