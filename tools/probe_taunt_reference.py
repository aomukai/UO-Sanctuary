#!/usr/bin/env python3
"""Compile Taunt do/target/AI/curse transitions using existing curse-probe services."""
from pathlib import Path
import hashlib,json,runpy,subprocess
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'
g=runpy.run_path(str(ROOT/'tools/probe_battlecry_reference.py'));source=g['source'];routine=g['routine'];blocks=g['blocks'];hashes=g['hashes'];tree=g['tree']
bar=source('source/D2Game/src/SKILLS/SkillBar.cpp');think=source('source/D2Game/src/AI/AiThink.cpp');util=source('source/D2Game/src/AI/AiUtil.cpp');tactics=source('source/D2Game/src/AI/AiTactics.cpp');mode=source('source/D2Game/src/MONSTER/MonsterMode.cpp')
cpp=g['cpp'].split('int main(){')[0]
cpp=cpp.replace('gdwBitMasks[]={1,2,4,8}', 'gdwBitMasks[]={1,2,4,8,16,32,64,128,256,512}')
cpp=cpp.replace('struct D2StatListStrc{','struct D2UnitStrc;struct D2StatListStrc{void(*callback)(D2UnitStrc*,int,D2StatListStrc*)=nullptr;')
cpp=cpp.replace('struct D2UnitStrc{','struct D2AiParamStrc{};struct D2AiControlStrc{int dwAiParam[3]={},dwOwnerGUID=-1,dwOwnerType=1,nAiSpecialState=0;};struct MonsterData{D2AiControlStrc* pAiControl=nullptr;D2AiParamStrc* pAiParam=nullptr;};struct D2UnitStrc{MonsterData* pMonsterData=nullptr;D2UnitStrc* target=nullptr;int x=0,y=0,alignment=1,monType=0;bool canSwitch=true,dead=false,melee=false;')
cpp=cpp.replace('struct D2MonStats2Txt{uint32_t dwFlags=1;}', 'struct D2MonStats2Txt{uint32_t dwFlags=1,dwModeFlags=4;}')
cpp=cpp.replace('using Callback=void(*)(D2UnitStrc*);','using Callback=void(*)(D2UnitStrc*,int,D2StatListStrc*);')
cpp=cpp.replace('int nStatesTxtRecordCount=256;', 'int nStatesTxtRecordCount=256,nItemStatCostTxtRecordCount=32;')
cpp=cpp.replace('void STATLIST_SetStatRemoveCallback(D2StatListStrc*,Callback){}void sub_6FD10E50(D2UnitStrc*){}','void STATLIST_SetStatRemoveCallback(D2StatListStrc* p,Callback f){p->callback=f;}void sub_6FD10E50(D2UnitStrc*,int,D2StatListStrc*){}')
cpp=cpp.replace('bool SKILLS_GetItemStatCostTxtRecord(int s){return s>=0;}','struct D2ItemStatCostTxt{unsigned dwItemStatFlags=0;} itemStat;D2ItemStatCostTxt* SKILLS_GetItemStatCostTxtRecord(int s){return s>=0?&itemStat:nullptr;}')
cpp=cpp.replace('return f==1?300+60*(r-1):f==2?-50-2*(r-1):f==3?-25-(r-1):0;', 'return f==1?0:f==2||f==3?-5-2*(r-1):0;')
cpp=cpp.replace('int CLIENTS_GetUnitX(D2UnitStrc*){return 0;}int CLIENTS_GetUnitY(D2UnitStrc*){return 0;}','int CLIENTS_GetUnitX(D2UnitStrc* p){return p->x;}int CLIENTS_GetUnitY(D2UnitStrc* p){return p->y;}')
# The shared hit filter calls this stub in branches outside Taunt's 0x8783 path.
cpp=cpp.replace('bool UNITS_IsInMeleeRange(D2UnitStrc*,D2UnitStrc*,int){return true;}','bool UNITS_IsInMeleeRange(D2UnitStrc*,D2UnitStrc* p,int){return p->melee;}')
cpp=r'#include <climits>\n#include <vector>\n'.replace('\\n','\n')+cpp
cpp+=r'''
#define D2_MAYBE_UNUSED(x) (void)(x)
using D2C_AiSpecialState=int;using D2C_States=int;using D2C_UnitEventTypes=int;
constexpr int STATE_TAUNT=27,STATE_TERROR=56,STATE_DIMVISION=23,AISPECIALSTATE_NONE=0,AISPECIALSTATE_TAUNT=12,AISPECIALSTATE_TERROR=11,AISPECIALSTATE_DIMVISION=10,UNIT_ALIGNMENT_NEUTRAL=0,UNITFLAG_TARGETABLE=4,STAT_DAMAGERESIST=20,STAT_MAGICRESIST=21,STAT_FIRERESIST=22,STAT_LIGHTRESIST=23,STAT_COLDRESIST=24,STAT_POISONRESIST=25,EVENTTYPE_AITHINK=1,MONMODE_ATTACK1=4;
struct D2UnkNecSkillStrc{int nDuration=0;D2GameStrc* pGame=nullptr;int nSkillId=0;D2UnitStrc* pUnit=nullptr;int unk0x08=0,nSkillLevel=0,bUpdateAnimRate=0,nAuraStat[6]={},nAuraStatCalc[6]={},nAuraTargetState=0,nAuraEvent[3]={},nAuraEventFunc[3]={};};
struct D2AuraCallbackStrc{void* pArgs=nullptr;D2UnitStrc* pOwner=nullptr;D2GameStrc* pGame=nullptr;int nCounter=0;};
struct D2_6FD106F0_Strc{D2UnitStrc* pUnit=nullptr;int nDistance=0,nX=0,nY=0;int(*pCallback)(D2GameStrc*,D2UnitStrc*)=nullptr;};
struct D2AiTickParamStrc{D2AiControlStrc* pAiControl=nullptr;};
std::vector<D2UnitStrc*> candidates;D2UnitStrc* acquired=nullptr;bool acquiredCombat=false;int switches=0,walks=0,attacks=0,idle=0,deleted=0,walkFlags=0,velocityMode=0;D2UnitStrc* attacked=nullptr;
D2AiControlStrc* AIGENERAL_GetAiControlFromUnit(D2UnitStrc* p){return p&&p->pMonsterData?p->pMonsterData->pAiControl:nullptr;}
int sub_6FD15190(D2UnitStrc* p,int){return p&&p->canSwitch;}
bool SUNIT_IsDead(D2UnitStrc* p){return !p||p->dead;}
void AIGENERAL_GetOwnerData(D2UnitStrc* p,int* id,int* type){auto* c=AIGENERAL_GetAiControlFromUnit(p);*id=c?c->dwOwnerGUID:-1;*type=c?c->dwOwnerType:1;}
D2UnitStrc* SUNIT_GetTargetUnit(D2GameStrc*,D2UnitStrc* p){return p->target;}
void UNITS_SetTargetUnitForDynamicUnit(D2UnitStrc* p,D2UnitStrc* q){p->target=q;}
int STATLIST_GetUnitAlignment(D2UnitStrc* p){return p->alignment;}
int STATLIST_GetUnitBaseStat(D2UnitStrc* p,int s,int){return p->stats[s];}
int MONSTERS_GetHirelingTypeId(D2UnitStrc*){return 0;}
int MONSTERUNIQUE_CheckMonTypeFlag(D2UnitStrc* p,unsigned f){return (p->monType&f)!=0;}
void AITHINK_ExecuteAiFn(D2GameStrc*,D2UnitStrc*,D2AiControlStrc* c,int state){if(c){c->nAiSpecialState=state;for(int& n:c->dwAiParam)n=0;switches++;}}
void SUNITEVENT_Unregister(D2GameStrc*,D2UnitStrc*,int,int){}
void sub_6FD156A0(D2GameStrc*,D2UnitStrc*,int,int,int,int,int,int){}
void D2GAME_EVENTS_Delete_6FC34840(D2GameStrc*,D2UnitStrc*,int,int){deleted++;}
void AITACTICS_Idle(D2GameStrc*,D2UnitStrc*,int n){idle=n;}
struct D2ModeChangeStrc{D2UnitStrc* pUnit=nullptr;D2UnitStrc* pTargetUnit=nullptr;};void MONSTERMODE_GetModeChangeInfo(D2UnitStrc* p,int,D2ModeChangeStrc* m){m->pUnit=p;}int D2GAME_ModeChange_6FC65220(D2GameStrc*,D2ModeChangeStrc* m,int){attacks++;attacked=m->pTargetUnit;m->pUnit->target=m->pTargetUnit;return 1;}
D2UnitStrc* sub_6FCCF9D0(D2GameStrc*,D2UnitStrc*,D2AiControlStrc*,int* d,int* combat){*d=1;*combat=acquiredCombat;return acquired;}
void D2GAME_MONSTERMODE_SetVelocityParams_6FC62FC0(D2AiParamStrc*,int n,int,int){velocityMode=n;}
int AITACTICS_WalkToTargetUnitWithFlags(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,unsigned char flags){walks++;walkFlags=flags;return 1;}
void sub_6FD0FE80(D2GameStrc* game,D2UnitStrc* p,int,int,int range,unsigned filter,int(*callback)(D2AuraCallbackStrc*,D2UnitStrc*),void* args,int,const char*,int){D2AuraCallbackStrc data{args,p,game,0};for(auto* q:candidates){int x=q->x-p->x,y=q->y-p->y;if(q!=p&&x*x+y*y<=range*range&&sub_6FD0FA00(p,q,filter))callback(&data,q);}}
'''
for text,name in [(g['skills'],'sub_6FD106F0'),(g['skills'],'SKILLS_FindAuraTarget'),(g['nec'],'AiSpecialStateFromAuraState'),(g['nec'],'sub_6FD0B250'),(g['nec'],'sub_6FD0B2B0'),(g['nec'],'sub_6FD0B450'),(think,'AITHINK_SetAiControlParams'),(util,'AIUTIL_SetOwnerGUIDAndType'),(bar,'SKILLS_Callback_FindTargetForTaunt'),(bar,'SKILLS_SrvDo071_Taunt'),(tactics,'sub_6FCD0410'),(tactics,'AITACTICS_ChangeModeAndTargetUnit'),(think,'D2GAME_AI_SpecialState12_6FCE81B0')]:cpp+=routine(text,name)+'\n'
cpp+=r'''int main(){D2AiControlStrc ctl;D2AiParamStrc params;MonsterData data{&ctl,&params};D2UnitStrc caster,target,alt;caster.dwUnitType=0;caster.dwUnitId=7;caster.room=target.room=alt.room=&room;target.pMonsterData=&data;target.dwFlags=7;row.wAuraTargetState=27;row.wAuraStat[0]=19;row.wAuraStat[1]=16;modes.dwModeFlags=4;
bool first=true;std::printf("{\"castCases\":[");
for(int rank:{1,20,21})for(int old:{0,1,21})for(int attract:{0,1})for(int selected:{0,1}){caster.target=selected?&target:nullptr;candidates=selected?std::vector<D2UnitStrc*>{}:std::vector<D2UnitStrc*>{&target};target.attract=attract;storage={};storage.skill=137;storage.state=27;storage.rank=old;storage.creator=999;storage.stats[19]=-5-2*(old-1);storage.stats[16]=-5-2*(old-1);target.list=old?&storage:nullptr;ctl={};ctl.dwAiParam[0]=9;switches=deleted=0;int result=SKILLS_SrvDo071_Taunt(&game,&caster,137,rank);auto* p=target.list;bool accepted=!attract&&(!old||rank>=old);if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",rank,old,attract,selected,result,switches,ctl.dwAiParam[0],p?p->rank:0,p?p->expire:0,p?p->creator:0,p?p->stats[19]:0,target.target==&caster);}
std::printf("],\"callbackCases\":[");first=true;for(int type:{0,1})for(int dead:{0,1})for(int eligible:{0,1})for(int owner:{0,1}){target.dwUnitType=type;target.dead=dead;target.canSwitch=eligible;ctl.dwOwnerGUID=owner?7:-1;ctl.dwOwnerType=owner?0:1;int result=SKILLS_Callback_FindTargetForTaunt(&game,&target);if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d]",type,dead,eligible,owner,result);}
target.dwUnitType=1;target.dead=false;target.canSwitch=true;ctl.dwOwnerGUID=-1;ctl.dwOwnerType=1;
std::printf("],\"selectionCases\":[");first=true;for(int x:{-21,-20,-5,0,5,20,21})for(int y:{0,1,5,20})for(int order:{0,1}){target.x=x;target.y=y;alt.x=7;alt.y=0;alt.pMonsterData=nullptr;alt.dwFlags=7;candidates=order?std::vector<D2UnitStrc*>{&alt,&target}:std::vector<D2UnitStrc*>{&target,&alt};auto* p=SKILLS_FindAuraTarget(&game,&caster,20,SKILLS_Callback_FindTargetForTaunt);if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d]",x,y,order,p==&target);}
std::printf("],\"aiCases\":[");first=true;D2AiTickParamStrc tick{&ctl};for(int present:{0,1})for(int town:{0,1})for(int started:{0,1})for(int melee:{0,1})for(int nearby:{0,1})for(int combat:{0,1}){target.target=present?&caster:nullptr;room.town=town;caster.melee=melee;acquired=nearby?&alt:nullptr;acquiredCombat=combat;ctl.dwAiParam[0]=started;switches=walks=attacks=idle=velocityMode=0;attacked=nullptr;D2GAME_AI_SpecialState12_6FCE81B0(&game,&target,&tick);if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",present,town,started,melee,nearby,combat,switches,walks,attacks,idle,ctl.dwAiParam[0],attacked==&alt,velocityMode);}
std::printf("],\"sentinelCases\":[");first=true;for(int a:{-666,0,1})for(int b:{-666,0,1})for(int c:{-666,0,1}){ctl.dwAiParam[0]=7;ctl.dwAiParam[1]=8;ctl.dwAiParam[2]=9;AITHINK_SetAiControlParams(&ctl,a,b,c);if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d]",a,b,c,ctl.dwAiParam[0],ctl.dwAiParam[1],ctl.dwAiParam[2]);}
std::printf("]}\n");}
'''
path=STAGE/'taunt-source-probe.cpp';path.write_text(cpp);binary=STAGE/'taunt-source-probe';subprocess.run(['g++','-std=c++20','-O2',str(path),'-o',str(binary)],check=True)
data=json.loads(subprocess.check_output([str(binary)]))
for rank,old,attract,selected,result,switches,phase,stored,expiry,creator,ar,target in data['castCases']:
 accept=not attract and (not old or rank>=old);replace=accept and (not old or rank>old)
 assert result==target==1 and switches==int(accept) and phase==(0 if accept else 9)
 assert (stored,creator)==((rank,7 if replace else 999) if accept else (old,999) if old else (0,0))
 if accept:assert expiry==0 and ar==-5-2*(rank-1)
for typ,dead,eligible,owner,result in data['callbackCases']:assert result==int(typ==1 and not dead and eligible and not owner)
for x,y,order,target in data['selectionCases']:assert target==int(x*x+y*y<49 or x*x+y*y==49 and not order)
for present,town,started,melee,nearby,combat,switches,walks,attacks,idle,phase,alternative,velocity in data['aiCases']:
 if not present or town:assert (switches,walks,attacks,idle,phase)==(1,0,0,1,0)
 elif not started:assert (walks,attacks,phase,velocity)==(1,0,1,13)
 elif melee:assert (walks,attacks,phase,alternative)==(0,1,1,0)
 elif nearby and combat:assert (walks,attacks,phase,alternative)==(0,1,0,1)
 else:assert (walks,attacks,phase,velocity)==(1,0,1,13)
for a,b,c,x,y,z in data['sentinelCases']:assert (x,y,z)==tuple(old if new==-666 else new for old,new in zip((7,8,9),(a,b,c)))
(STAGE/'taunt-source-probe.json').write_text(json.dumps(data,indent=2)+'\n')
for text,name in [(g['skills'],'sub_6FD0FE80'),(g['damage'],'SUNITDMG_IsHitSuccessful'),(think,'AITHINK_ExecuteAiFn'),(g['skills'],'D2GAME_SKILLS_Handler_6FD12BA0'),(mode,'D2GAME_ModeChange_6FC65220')]:
 try:routine(text,name)
 except AssertionError:
  raise
out=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],sourceHashes=hashes,probe=dict(caseCounts={k:len(v) for k,v in data.items()},blockHashes={k:hashlib.sha256(v.encode()).hexdigest() for k,v in blocks.items()},dataPath='.local/class-skills/taunt-source-probe.json'),established=[
'Do71 validates or finds a living AI-switchable monster without a player owner; fallback is caster-nearest squared distance within20 source units/filter0x8783, strict-smaller ties retain scan order.',
'Full shared AI curse writer compiles with generic curse allocation: no invented duration; same-rank refresh retains creator, stronger replaces, lower rejects. Taunt do still returns success and updates dynamic target/scheduling when allocation fails.',
'Actual source AI12 initial phase approaches even if already in melee; later attacks dynamic taunter in melee or nearby acquired melee target and resets phase, otherwise restores approach. Missing/town target returns to ordinary AI/idles1frame without removing the untimed curse.',
'Sentinel-666 preserves AI parameters; successful generic AI switch separately initializes parameters to0 through the declared switch fixture. Lower-rank failed allocation retains the prior phase while replacing dynamic taunter target.',
'The compiled attack wrapper uses a declared mode-change service. Reviewed mode-change code binds pTargetUnit into the dynamic path. Attacking a nearby acquired target can therefore replace the original dynamic taunter; subsequent approach must retain that target rather than unconditionally restoring the caster.',
'Owner helper clears AI owner GUID=-1/type1. Approach helper sets velocity parameter13,0,0 and walks withflags7. Curse removal callback reselects ordinary AI before ordinary state cleanup.',
'Untimed item_tohit_percent and damagepercent belong to shared AR/weapon ED pools. NPC source hit routine joins percentage before chance; negative-AR special handling remains source/native acceptance beyond ordinary supported ranks.'],stubs=[
'Reuses the compiled Battle Cry generic-curse/filter/stat fixture instead of a second contradictory service model. Compiled do71/callback/search callback/full AI writer/removal/AI12/sentinel/owner/approach routines are unchanged extracted source.',
'Room scan supplies explicit candidates with filter and squared radius; complete source room traversal is hash-reviewed. AI eligibility, acquired threat/melee, event scheduling, stat/event/allocation services and ordinary-AI switch are declared fixtures.',
'AI switch fixture initializes parameters for the mapped no-exit-callback case; full transition implementation is reviewed, not linked. Species-specific AI exit callbacks, world movement/path/scan and modern executable behavior are not certified.'],limits=[
'Bounded supplementary source proof; native consumer is verified separately in taunt-native-evidence.json and this is not a modern parity certificate. Native target eligibility/geometry/SC/FCR/item/owner/client and untimed state lifecycle require separate checks.',
'Attract/curse-resistance failed-allocation transient dynamic-target changes and source owner graphs must remain explicit if unsupported. Do not charge mana on a no-target do0 path or invent a timed expiry.',
'Native existing basic hit-chance/AR projections, negative AR and exotic item/cast/PvP/elite branches remain open.'])
(ROOT/'tools/reference/taunt-engine-evidence.json').write_text(json.dumps(out,indent=2)+'\n');print('Taunt bounded source probes passed:',out['probe']['caseCounts'])
