#!/usr/bin/env python3
"""Compose unchanged AI101, sequence, mode14 and neutral routines frame by frame.

Constrained mode-entry/event services expose explicit boundaries. This is not
execution of the full game scheduler or proof of modern executable equivalence.
"""
from pathlib import Path
import argparse, hashlib, json, re, subprocess, sys
ROOT=Path(__file__).resolve().parents[1]; STAGE=ROOT/'.local/class-skills'
p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');args=p.parse_args()
subprocess.run([sys.executable,str(ROOT/'tools/probe_sentry_sequence_reference.py'),'--check'],check=True,stdout=subprocess.DEVNULL)
tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());hashes={};routines={}
def source(rel):
 raw=(STAGE/'d2moo-pinned-source'/rel).read_bytes()
 assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel)
 hashes[rel]=hashlib.sha256(raw).hexdigest();return raw.decode()
def extract(s,name):
 m=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',s,re.M);assert m,name;end=s.index('{',m.start())+1;depth=1
 while depth:depth+=(s[end]=='{')-(s[end]=='}');end+=1
 body=s[m.start():end];routines[name]=hashlib.sha256(body.encode()).hexdigest();return body
mode=source('source/D2Game/src/MONSTER/MonsterMode.cpp');ai=source('source/D2Game/src/AI/AiThink.cpp');unit=source('source/D2Game/src/UNIT/SUnit.cpp');events=source('source/D2Game/src/GAME/Event.cpp')
# Exact table/selector assertions establish which scheduler mode14 uses.
table=mode.split('constexpr D2MonModeInfoTableStrc stru_6FD28738[16] =',1)[1].split('};',1)[0]
rows=re.findall(r'\{\s*(\d),\s*(\d),\s*(\d)\s*\}',table);assert len(rows)==16 and rows[14]==('1','0','0')
assert 'else\n            {\n                sub_6FCBD3A0(pGame, pUnit);\n            }' in mode
assert 'pInfinite[nMonsterEventIdx], 0' in events and events.index('pInfinite[nMonsterEventIdx], 0')<events.index('pHead[nMonsterEventIdx]')
cpp=(STAGE/'sentry-sequence-source-probe.cpp').read_text().split('int main()',1)[0]
cpp=cpp.replace('struct D2UnitStrc{','struct Monstats{int nSkill[1]={306};int nAIdel[3]={15,15,15};};struct MonsterData{Monstats* pMonstatsTxt;};\nstruct D2UnitStrc{int dwAnimMode=1,dwUnitType=1;MonsterData* pMonsterData=nullptr;')
cpp+=r'''
using D2MonStatsTxt=Monstats;
struct D2GameStrc{int dwGameFrame=0,nGameType=0,dwGameType=0,nDifficulty=0;};
struct D2ModeChangeStrc{D2UnitStrc* pUnit;};struct D2ActiveRoomStrc{};
struct D2SkillStrc{int flags=0;};struct D2SkillsTxt{int dwCalc[4]={0,0,0,1};};
struct D2AiControlStrc{int dwAiParam[2]={0,-1};};struct D2AiTickParamStrc{D2AiControlStrc* pAiControl;Monstats* pMonstatsTxt;};
constexpr int STATE_INFERNO=1,STATE_STUNNED=2,EVENTTYPE_AITHINK=2,EVENTTYPE_MODECHANGE=0,UNIT_MONSTER=1;
enum D2C_AssassinSentryAIParams{ASSASSINSENTRY_AI_PARAM_ATTACK_CHANCE_PCT=0,ASSASSINSENTRY_AI_PARAM_STALL_DURATION=1,ASSASSINSENTRY_AI_PARAM_INACTIVE_STALL_DURATION=2,ASSASSINSENTRY_AI_PARAM_ACTIVE_DISTANCE=3};
D2UnitStrc owner,targetUnit;D2ActiveRoomStrc room;D2SkillStrc skill;D2SkillsTxt skillRow;
int nextAi=25,thinks=0,attempts=0,dispatches=0,shots=5,targetStyle=0,chanceStyle=0,startStyle=0,environment=0;
bool perFrame=false,validOwner=true,validRoom=true,town=false;
void SUNIT_SetCombatMode(D2GameStrc*,D2UnitStrc* u,int mode){u->dwAnimMode=mode;if(mode==14)UNITS_InitializeSequence(u);else u->pAnimSeq=nullptr;}
int EVENT_GetEventFrame(D2GameStrc*,D2UnitStrc*,int){return nextAi;}
void EVENT_SetEvent(D2GameStrc*,D2UnitStrc*,int type,int frame,int,int){assert(type==EVENTTYPE_AITHINK);nextAi=frame;}
bool STATES_CheckState(D2UnitStrc*,int){return false;}
void STATES_ToggleState(D2UnitStrc*,int,int){assert(false);}
void D2GAME_EVENTS_Delete_6FC34840(D2GameStrc*,D2UnitStrc*,int type,int){if(type==EVENTTYPE_AITHINK)nextAi=0;else if(type==EVENTTYPE_MODECHANGE)perFrame=false;else assert(false);}
void sub_6FC35570(D2GameStrc*,D2UnitStrc*,int type,int,int){assert(type==EVENTTYPE_MODECHANGE);perFrame=true;}
int sub_6FD12FD0(D2GameStrc* g,D2UnitStrc*){return startStyle==0||g->dwGameFrame%2==0;}
D2SkillStrc* UNITS_GetUsedSkill(D2UnitStrc*){return &skill;}
int SKILLS_GetFlags(D2SkillStrc* s){return s->flags;}void SKILLS_SetFlags(D2SkillStrc* s,int f){s->flags=f;}
void sub_6FCBC7E0(D2GameStrc*,D2UnitStrc*){assert(false);}int sub_6FCBC930(D2GameStrc*,D2UnitStrc*){assert(false);return 0;}
void sub_6FD13410(D2GameStrc*,D2UnitStrc* u){dispatches++;u->dwFlags|=64;}
int sub_6FC64310(D2GameStrc*,D2ModeChangeStrc*);
void sub_6FC63680(D2GameStrc* g,D2UnitStrc* u){D2ModeChangeStrc m{u};sub_6FC64310(g,&m);perFrame=false;}
D2UnitStrc* AIGENERAL_GetMinionOwner(D2UnitStrc*){return validOwner?&owner:nullptr;}
D2ActiveRoomStrc* UNITS_GetRoom(D2UnitStrc*){return validRoom?&room:nullptr;}
bool DUNGEON_IsRoomInTown(D2ActiveRoomStrc*){return town;}
D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int){return &skillRow;}
D2SkillStrc* SKILLS_GetHighestLevelSkillFromUnitAndId(D2UnitStrc*,int){return &skill;}
int SKILLS_GetSkillLevel(D2UnitStrc*,D2SkillStrc*,int){return 1;}
int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int,int,int){return shots;}
void AITACTICS_ChangeModeAndTargetCoordinates(D2GameStrc*,D2UnitStrc* u,int mode,int,int){assert(!mode);u->dwAnimMode=0;perFrame=false;nextAi=0;}
D2UnitStrc* sub_6FCF2CC0(D2GameStrc* g,D2UnitStrc*,int* distance,int* melee){*melee=0;*distance=targetStyle==2?25:10;return targetStyle==1||(targetStyle==3&&g->dwGameFrame<40)?nullptr:&targetUnit;}
int AI_GetParamValue(D2GameStrc*,D2AiTickParamStrc*,int index){int params[]={100,10,15,25};return params[index];}
bool AIRollChanceParam(D2GameStrc* g,D2UnitStrc*,D2AiTickParamStrc*,int){return chanceStyle==0||g->dwGameFrame%2==0;}
void AITACTICS_IdleInNeutralMode(D2GameStrc* g,D2UnitStrc* u,int delay){D2ModeChangeStrc m{u};sub_6FC64310(g,&m);perFrame=false;nextAi=g->dwGameFrame+delay;}
int SKILLS_GetSkillMode(D2SkillStrc*){return 14;}
int sub_6FC64E60(D2GameStrc*,D2ModeChangeStrc*);void sub_6FCBD3A0(D2GameStrc*,D2UnitStrc*);
void AITACTICS_UseSkill(D2GameStrc* g,D2UnitStrc* u,int mode,int id,D2UnitStrc* aim,int,int){assert(mode==14&&id==306&&aim==&targetUnit);attempts++;D2ModeChangeStrc m{u};if(sub_6FC64E60(g,&m))sub_6FCBD3A0(g,u);else AITACTICS_IdleInNeutralMode(g,u,10);}
int32_t AITHINK_AssasinSentryHasLostTarget(D2GameStrc*,D2UnitStrc*,D2AiTickParamStrc*,int32_t);
'''
for name in ['sub_6FC64310','sub_6FC64E60','sub_6FC64E90']:cpp+='\n'+extract(mode,name)+'\n'
cpp+='\n'+extract(unit,'sub_6FCBD3A0')+'\n'
for name in ['AITHINK_AssasinSentryHasLostTarget','AITHINK_Fn101_AssassinSentry']:cpp+='\n'+extract(ai,name)+'\n'
cpp+=r'''
int main(){for(int i=0;i<15;i++)rows[i]={9u,unsigned(i),0,i==2?1:0};printf("[");bool first=true;
for(int count:{1,5,10})for(int t=0;t<4;t++)for(int chance=0;chance<2;chance++)for(int start=0;start<2;start++)for(int env=0;env<4;env++){
 D2GameStrc g;D2UnitStrc u;Monstats mon;MonsterData data{&mon};u.pMonsterData=&data;D2AiControlStrc control;D2AiTickParamStrc tick{&control,&mon};
 shots=count;targetStyle=t;chanceStyle=chance;startStyle=start;environment=env;nextAi=25;perFrame=false;thinks=attempts=dispatches=0;skill.flags=0;
 for(int f=1;f<=220;f++){g.dwGameFrame=f;validOwner=env!=1||f<70;validRoom=env!=2||f<70;town=env==3&&f>=70;
 if(perFrame)sub_6FC64E90(&g,&u);
 if(nextAi==f&&u.dwAnimMode!=0){nextAi=0;thinks++;AITHINK_Fn101_AssassinSentry(&g,&u,&tick);}
 bool seq=u.dwAnimMode==14;
 printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%u,%d,%d,%d,%d,%d,%d]",first?"":",",count,t,chance,start,env,f,u.dwAnimMode,nextAi,control.dwAiParam[1],seq?u.dwSeqFrame:0,seq?u.dwFrameCountPrecise:0,seq?u.nActionFrame:0,(u.dwFlags&64)!=0,thinks,attempts,dispatches);first=false;
 }}printf("]\n");}
'''
path=STAGE/'sentry-controller-source-probe.cpp';path.write_text(cpp);binary=path.with_suffix('')
subprocess.run(['g++','-std=c++17','-O2',str(path),'-o',str(binary)],check=True)
result=json.loads(subprocess.check_output([str(binary)]));assert len(result)==42240
san=STAGE/'sentry-controller-source-probe-san';subprocess.run(['g++','-std=c++17','-g','-fsanitize=address,undefined','-fno-omit-frame-pointer',str(path),'-o',str(san)],check=True)
with (STAGE/'sentry-controller-sanitizer.log').open('wb') as log:clean=json.loads(subprocess.check_output([str(san)],stderr=log))
assert clean==result and not (STAGE/'sentry-controller-sanitizer.log').read_bytes()
fixture=STAGE/'sentry-controller-source-probe.json';fixture.write_text(json.dumps(result,separators=(',',':'))+'\n')
e={'referenceVersion':'3.3.93847','legacyCommit':tree['sha'],'sourceHashes':hashes,'routineHashes':routines,'sequenceEvidence':'tools/reference/sentry-sequence-source-evidence.json','verification':{'contexts':192,'frames':len(result)},'fixture':{'path':str(fixture.relative_to(ROOT)),'sha256':hashlib.sha256(fixture.read_bytes()).hexdigest()},'established':['Mode info14 selects infinite per-frame MODECHANGE, not sparse sub_6FCBCE70. Infinite monster phase precedes scheduled AI phase.','Complete AI101/mode14/neutral/sequence routines compose across target absence, distance25, delayed targets, explicit chance/start failures and owner/room/town exits.','At retained sequence speed256, successful attempts dispatch on the third subsequent phase, reach neutral on phase16 and schedule the next think15 frames later. Final shot dispatch precedes exhausted-shot exit.'],'limits':['This constrained harness declares mode-entry/start/do/target/room/skill/chance and event-list services; the full mode change, scheduler and modern executable are not executed.','Initial neutral/AI+25 is an explicit prepared-entry condition. Player do45 S1 emergence, live target selector, sequence-speed provenance and native projectile combat remain separate.','No independent idle expiry is inferred. Native offline/facet rules are approved Sanctuary adapters, not asserted D2R behavior.'],'handlerEnabled':False}
epath=ROOT/'tools/reference/sentry-controller-source-evidence.json'
if args.check:assert json.loads(epath.read_text())==e
else:epath.write_text(json.dumps(e,indent=2)+'\n')
print('192 composed source contexts /42240 frames; ASan/UBSan clean; declared service boundaries retained')
