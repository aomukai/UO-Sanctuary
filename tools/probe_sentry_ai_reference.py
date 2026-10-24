#!/usr/bin/env python3
"""Execute complete pinned Assassin Sentry AI101 and shot accounting routines.

Owner/room, skill/formula, target/distance, attack roll and mode/event consumers
are explicit fixtures. This does not establish current executable equivalence.
"""
from pathlib import Path
import argparse, hashlib, json, re, subprocess
ROOT=Path(__file__).resolve().parents[1]; STAGE=ROOT/'.local/class-skills'
p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');args=p.parse_args()
tree=json.loads((STAGE/'d2moo-source-tree.json').read_text())
rel='source/D2Game/src/AI/AiThink.cpp';raw=(STAGE/'d2moo-pinned-source'/rel).read_bytes()
assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel)
s=raw.decode();routines={}
def extract(name):
 m=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',s,re.M);assert m,name
 end=s.index('{',m.start())+1;depth=1
 while depth:depth+=(s[end]=='{')-(s[end]=='}');end+=1
 body=s[m.start():end];routines[name]=hashlib.sha256(body.encode()).hexdigest();return body
cpp=r'''
#include <cassert>
#include <cstdint>
#include <cstdio>
#define __fastcall
struct D2GameStrc{int dwGameFrame=42;};struct D2UnitStrc{};struct D2ActiveRoomStrc{};
struct D2SkillStrc{};struct D2SkillsTxt{int dwCalc[4]={0,0,0,1};};
struct D2AiControlStrc{int dwAiParam[2]={0,0};};struct Monstats{int nSkill[1]={306};};
struct D2AiTickParamStrc{D2AiControlStrc* pAiControl;Monstats* pMonstatsTxt;};
constexpr int STATE_INFERNO=1,EVENTTYPE_AITHINK=2;
D2UnitStrc owner,target;D2ActiveRoomStrc room;D2SkillStrc skill;D2SkillsTxt record;
bool hasOwner,hasRoom,town,hasRow,hasSkill,hasTarget,chance,inferno;
int distanceValue,formulaValue,action,delayValue,deletes,toggles,uses;
D2UnitStrc* AIGENERAL_GetMinionOwner(D2UnitStrc*){return hasOwner?&owner:nullptr;}
D2ActiveRoomStrc* UNITS_GetRoom(D2UnitStrc*){return hasRoom?&room:nullptr;}
bool DUNGEON_IsRoomInTown(D2ActiveRoomStrc*){return town;}
D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int id){assert(id==306);return hasRow?&record:nullptr;}
D2SkillStrc* SKILLS_GetHighestLevelSkillFromUnitAndId(D2UnitStrc*,int id){assert(id==306);return hasSkill?&skill:nullptr;}
int SKILLS_GetSkillLevel(D2UnitStrc*,D2SkillStrc*,int){return 3;}
int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int code,int id,int rank){assert(code==1&&id==306&&rank==3);return formulaValue;}
void AITACTICS_ChangeModeAndTargetCoordinates(D2GameStrc*,D2UnitStrc*,int mode,int x,int y){assert(!mode&&!x&&!y);action=1;}
bool STATES_CheckState(D2UnitStrc*,int){return inferno;}
void STATES_ToggleState(D2UnitStrc*,int,int value){assert(value==0);inferno=false;toggles++;}
D2UnitStrc* sub_6FCF2CC0(D2GameStrc*,D2UnitStrc*,int* distance,int* melee){*distance=distanceValue;*melee=0;return hasTarget?&target:nullptr;}
int AI_GetParamValue(D2GameStrc*,D2AiTickParamStrc*,int index){int params[]={100,10,15,25};assert(index>=0&&index<4);return params[index];}
void AITACTICS_IdleInNeutralMode(D2GameStrc*,D2UnitStrc*,int delay){action=2;delayValue=delay;}
bool AIRollChanceParam(D2GameStrc*,D2UnitStrc*,D2AiTickParamStrc*,int index){assert(index==0);return chance;}
void D2GAME_EVENTS_Delete_6FC34840(D2GameStrc*,D2UnitStrc*,int event,int extra){assert(event==EVENTTYPE_AITHINK&&!extra);deletes++;}
int SKILLS_GetSkillMode(D2SkillStrc*){return 14;}
void AITACTICS_UseSkill(D2GameStrc*,D2UnitStrc*,int mode,int id,D2UnitStrc* aim,int x,int y){assert(mode==14&&id==306&&aim==&target&&!x&&!y);action=3;uses++;}
int32_t AITHINK_AssasinSentryHasLostTarget(D2GameStrc*,D2UnitStrc*,D2AiTickParamStrc*,int32_t);
enum D2C_AssassinSentryAIParams{ASSASSINSENTRY_AI_PARAM_ATTACK_CHANCE_PCT=0,ASSASSINSENTRY_AI_PARAM_STALL_DURATION=1,ASSASSINSENTRY_AI_PARAM_INACTIVE_STALL_DURATION=2,ASSASSINSENTRY_AI_PARAM_ACTIVE_DISTANCE=3};
'''
for name in ['D2GAME_AI_Unk101_104_6FCE9CE0','AITHINK_AssasinSentryHasLostTarget','AITHINK_Fn101_AssassinSentry']:cpp+='\n'+extract(name)+'\n'
cpp+=r'''
int main(){
 D2GameStrc game;D2UnitStrc unit;D2AiControlStrc control;Monstats monster;D2AiTickParamStrc tick{&control,&monster};
 D2GAME_AI_Unk101_104_6FCE9CE0(&game,&unit,&tick);assert(control.dwAiParam[0]==42&&control.dwAiParam[1]==-1);
 int count=0;printf("{\"cases\":[");
 for(int mask=0;mask<256;mask++)for(int shots:{-1,0,1,2})for(int formula:{0,1,5})for(int distance:{0,24,25,26}){
  hasOwner=mask&1;hasRoom=mask&2;town=mask&4;hasRow=mask&8;hasSkill=mask&16;hasTarget=mask&32;chance=mask&64;inferno=mask&128;
  distanceValue=distance;formulaValue=formula;control.dwAiParam[1]=shots;action=delayValue=deletes=toggles=uses=0;
  int expectedShots=shots,expectedAction=1,expectedDelay=0,expectedToggle=0;
  bool valid=hasOwner&&hasRoom&&!town;
  if(valid&&expectedShots<0){if(!hasRow||!hasSkill){valid=false;}else{expectedShots=formula;}}
  if(valid&&expectedShots>0&&hasSkill){
   expectedToggle=inferno?1:0;
   if(!hasTarget||distance>=25){expectedAction=2;expectedDelay=15;}
   else if(chance){expectedAction=3;--expectedShots;}
   else{expectedAction=2;expectedDelay=10;}
  }
  AITHINK_Fn101_AssassinSentry(&game,&unit,&tick);
  assert(control.dwAiParam[1]==expectedShots&&action==expectedAction&&delayValue==expectedDelay&&toggles==expectedToggle);
  assert(deletes==(expectedAction==3)&&uses==(expectedAction==3));
  printf("%s[%d,%d,%d,%d,%d,%d,%d,%d]",count?",":"",mask,shots,formula,distance,control.dwAiParam[1],action,delayValue,toggles);count++;
 }
 printf("],\"verification\":{\"initializationCases\":1,\"completeAiCases\":%d}}\n",count);
}
'''
cpp=cpp.replace('#include <cstdio>','#include <cstdio>\n#include <initializer_list>')
source=STAGE/'sentry-ai-source-probe.cpp';source.write_text(cpp);binary=STAGE/'sentry-ai-source-probe'
subprocess.run(['g++','-std=c++17','-Wall','-Wextra','-Werror','-Wno-unused-parameter',str(source),'-o',str(binary)],check=True)
result=json.loads(subprocess.check_output([str(binary)]));counts=result['verification'];assert counts==dict(initializationCases=1,completeAiCases=12288)
san=STAGE/'sentry-ai-source-probe-san';subprocess.run(['g++','-std=c++17','-g','-fsanitize=address,undefined','-fno-omit-frame-pointer',str(source),'-o',str(san)],check=True)
with (STAGE/'sentry-ai-sanitizer.log').open('wb') as log:sanitized=subprocess.check_output([str(san)],stderr=log)
assert json.loads(sanitized)==result and not (STAGE/'sentry-ai-sanitizer.log').read_bytes()
fixture=STAGE/'sentry-ai-source-probe.json';fixture.write_text(json.dumps(result['cases'],separators=(',',':'))+'\n')
evidence={'fixture':{'path':str(fixture.relative_to(ROOT)),'sha256':hashlib.sha256(fixture.read_bytes()).hexdigest()},'referenceVersion':'3.3.93847','legacyCommit':'0c0bbd1e6bef9181a3da1c046523af622a925416','sourceHashes':{rel:hashlib.sha256(raw).hexdigest()},'routineHashes':routines,'verification':counts,'scope':'Complete unchanged AI101, initialization and lazy remaining-shot helper; clean ASan/UBSan. Owner/room, skill/formula, target/distance, attack roll and event/mode consumers are explicit fixtures.','established':['Lazy negative-shot initialization; invalid owner/room/town/skill and exhausted-shot mode exit.','Target distance25 exclusion, separate10/15 stalls, inferno state removal and pre-UseSkill shot debit with AI-event deletion. A last shot is dispatched with remaining count0.'],'limits':['No actor implementation, projectile creation/damage or current executable equivalence is established.','Sequence/mode timing and failures after UseSkill, native target selection/geometry, owner lifecycle and modern mastery/pierce/NextHit remain separate.','Attack chance is an explicit Boolean service; table100percent behavior does not imply arbitrary RNG outcomes occur in gameplay.'],'handlerEnabled':False}
path=ROOT/'tools/reference/sentry-ai-source-evidence.json'
if args.check:assert json.loads(path.read_text())==evidence
else:path.write_text(json.dumps(evidence,indent=2)+'\n')
print(json.dumps(counts),'complete pinned routines; sanitizers clean; service boundaries explicit')
