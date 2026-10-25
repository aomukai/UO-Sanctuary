#!/usr/bin/env python3
"""Compile pinned AI104 and its complete shared shot helper, with explicit services."""
from pathlib import Path
import argparse
import hashlib
import json
import re
import subprocess

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills'
parser = argparse.ArgumentParser()
parser.add_argument('--check', action='store_true')
args = parser.parse_args()
relative = 'source/D2Game/src/AI/AiThink.cpp'
raw = (STAGE / 'd2moo-pinned-source' / relative).read_bytes()
tree = json.loads((STAGE / 'd2moo-source-tree.json').read_text())
assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(
    row['sha'] for row in tree['tree'] if row['path'] == relative)
text = raw.decode()
routines = {}


def extract(name):
    match = re.search(r'^.*\b' + name + r'\([^;\n]*\)\s*\n\{', text, re.M)
    assert match, name
    end = text.index('{', match.start()) + 1
    depth = 1
    while depth:
        depth += (text[end] == '{') - (text[end] == '}')
        end += 1
    body = text[match.start():end]
    routines[name] = hashlib.sha256(body.encode()).hexdigest()
    return body


cpp = r'''
#include <cassert>
#include <cstdint>
#include <cstdio>
#include <initializer_list>
#define __fastcall
struct D2GameStrc{int dwGameFrame=42;};
struct D2UnitStrc{int dwUnitId=0;};struct D2ActiveRoomStrc{};struct D2SkillStrc{};
struct D2SkillsTxt{int dwCalc[4]={0,0,0,1};int dwParam[4]={40,80,10,1};};
struct D2AiControlStrc{int dwAiParam[2]={0,0};};
struct Monstats{int nSkill[2]={312,324};};
struct D2AiTickParamStrc{D2AiControlStrc* pAiControl;Monstats* pMonstatsTxt;};
constexpr int MONMODE_SKILL2=9,MONMODE_SEQUENCE=14;
enum D2C_DeathSentryAIParams{DEATHSENTRY_AI_PARAM_STALL_DURATION=1,
 DEATHSENTRY_AI_PARAM_SKILL_CHANCE_PCT=2,DEATHSENTRY_AI_PARAM_ACTIVE_DISTANCE=3};
D2UnitStrc owner{1},enemy{2},corpse{7};D2ActiveRoomStrc room;D2SkillStrc skill;D2SkillsTxt record;
bool hasOwner,hasRoom,town,hasRow,hasSkill,hasEnemy,hasCorpse,chance,loseOnDebit;
int rankValue,formulaValue,enemyDistance,corpseDistance,action,delayValue,uses,
 ownerQueries,corpseQueries,chanceQueries,shotsAtUse,lastAtUse;
D2AiControlStrc* activeControl;
D2UnitStrc* AIGENERAL_GetMinionOwner(D2UnitStrc*){
 ++ownerQueries;return hasOwner&&!(loseOnDebit&&ownerQueries>1)?&owner:nullptr;}
D2ActiveRoomStrc* UNITS_GetRoom(D2UnitStrc*){return hasRoom?&room:nullptr;}
bool DUNGEON_IsRoomInTown(D2ActiveRoomStrc*){return town;}
D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int id){assert(id==312);return hasRow?&record:nullptr;}
D2SkillStrc* SKILLS_GetHighestLevelSkillFromUnitAndId(D2UnitStrc*,int id){assert(id==312);return hasSkill?&skill:nullptr;}
int SKILLS_GetSkillLevel(D2UnitStrc*,D2SkillStrc*,int){return rankValue;}
int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int code,int id,int rank){
 assert(code==1&&id==312&&rank==rankValue);return formulaValue;}
void AITACTICS_ChangeModeAndTargetCoordinates(D2GameStrc*,D2UnitStrc*,int mode,int x,int y){
 assert(mode==0&&x==0&&y==0);action=1;}
D2UnitStrc* sub_6FCF2CC0(D2GameStrc*,D2UnitStrc*,int* distance,int* combat){
 *distance=enemyDistance;*combat=0;return hasEnemy?&enemy:nullptr;}
D2UnitStrc* sub_6FD15210(D2UnitStrc*,D2UnitStrc* target,int id,int rank){
 assert(target==&enemy&&id==312&&rank==rankValue);++corpseQueries;return hasCorpse?&corpse:nullptr;}
int UNITS_GetDistanceToOtherUnit(D2UnitStrc* a,D2UnitStrc* b){
 assert(a==&enemy&&b==&corpse);return corpseDistance;}
int AI_GetParamValue(D2GameStrc*,D2AiTickParamStrc*,int index){
 assert(index==1||index==3);return index==1?0:16;}
bool AIRollChanceParam(D2GameStrc*,D2UnitStrc*,D2AiTickParamStrc*,int index){
 assert(index==2);++chanceQueries;return chance;}
void AITACTICS_IdleInNeutralMode(D2GameStrc*,D2UnitStrc*,int delay){action=2;delayValue=delay;}
void AITACTICS_UseSkill(D2GameStrc*,D2UnitStrc*,int mode,int id,D2UnitStrc* target,int x,int y){
 assert(x==0&&y==0);assert((mode==9&&id==312&&target==&corpse)||(mode==14&&id==324&&target==&enemy));
 action=id==312?3:4;++uses;shotsAtUse=activeControl->dwAiParam[1];lastAtUse=activeControl->dwAiParam[0];}
'''
for name in ['D2GAME_AI_Unk101_104_6FCE9CE0', 'AITHINK_AssasinSentryHasLostTarget', 'AITHINK_Fn104_DeathSentry']:
    cpp += '\n' + extract(name) + '\n'
cpp += r'''
int main(){
 D2GameStrc game;D2UnitStrc actor;D2AiControlStrc control;Monstats monster;
 D2AiTickParamStrc tick{&control,&monster};activeControl=&control;
 D2GAME_AI_Unk101_104_6FCE9CE0(&game,&actor,&tick);
 assert(control.dwAiParam[0]==42&&control.dwAiParam[1]==-1);
 int count=0;std::printf("{\"cases\":[");
 auto run=[&](int mask,int shots,int formula,int rank,int distance,int cdistance,int last,bool lost){
  hasOwner=mask&1;hasRoom=mask&2;town=mask&4;hasRow=mask&8;
  hasSkill=mask&16;hasEnemy=mask&32;hasCorpse=mask&64;chance=mask&128;loseOnDebit=lost;
  formulaValue=formula;rankValue=rank;enemyDistance=distance;corpseDistance=cdistance;
  control.dwAiParam[0]=last;control.dwAiParam[1]=shots;
  action=delayValue=uses=ownerQueries=corpseQueries=chanceQueries=0;shotsAtUse=lastAtUse=-999;
  int expectedShots=shots,expectedLast=last,expectedAction=1,expectedQueries=0,expectedChance=0;
  bool valid=hasOwner&&hasRoom&&!town;
  if(valid&&shots<0){if(!hasRow||!hasSkill){valid=false;}else{expectedShots=formula;}}
  if(valid&&expectedShots>0){
   expectedAction=0;
   if(hasSkill&&rank>0){
    if(!hasEnemy){expectedAction=2;}
    else{
     expectedQueries=1;
     int radius=hasRow?(10+rank-1)/2:0;
     bool explode=hasCorpse&&last!=7&&cdistance<radius;
     if(explode){
      if(lost){expectedAction=1;}else{expectedAction=3;expectedLast=7;--expectedShots;}
     }else{
      expectedChance=distance<16?1:0;
      if(distance<16&&chance){if(lost){expectedAction=1;}else{expectedAction=4;--expectedShots;}}
      else{expectedAction=2;}
     }
    }
   }
  }
  AITHINK_Fn104_DeathSentry(&game,&actor,&tick);
  assert(control.dwAiParam[0]==expectedLast&&control.dwAiParam[1]==expectedShots&&action==expectedAction);
  assert(corpseQueries==expectedQueries&&chanceQueries==expectedChance&&delayValue==0);
  assert(uses==((action==3||action==4)?1:0));
  if(uses){assert(shotsAtUse==expectedShots&&lastAtUse==expectedLast);}
  std::printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",count?",":"",
   mask,shots,formula,rank,distance,cdistance,last,lost?1:0,control.dwAiParam[0],control.dwAiParam[1],action,
   ownerQueries,corpseQueries,chanceQueries,shotsAtUse,lastAtUse);++count;
 };
 // Invalid owner/room/town/row/skill, lazy initialization, exhausted and final shots.
 for(int mask=0;mask<256;++mask)for(int shots:{-1,0,1,2})for(int formula:{0,5,11})
  run(mask,shots,formula,3,15,5,0,false);
 int validity=count;
 // Corpse priority, repeated GUID, rank-dependent strict integer half-radius,
 // strict lightning range, 50-percent roll service and second owner recheck.
 for(int rank:{1,2,3,20,255})for(int distance:{0,15,16,17,40})for(int offset:{-1,0,1})
 for(int flags=0;flags<16;++flags)for(int shots:{1,2}){
  int mask=1|2|8|16|32|((flags&1)?64:0)|((flags&2)?128:0);
  run(mask,shots,5,rank,distance,(10+rank-1)/2+offset,(flags&4)?7:0,flags&8);
 }
 int thresholds=count-validity;
 // Skill/level disappears after remaining shots were initialized: source returns
 // without scheduling a substitute idle or death action.
 for(int mask:{1|2|8|32,1|2|8|16|32})for(int rank:{-1,0,1})for(int shots:{1,5})
  run(mask,shots,5,rank,0,0,0,false);
 std::printf("],\"verification\":{\"initializationCases\":1,\"validityCases\":%d,\"thresholdCases\":%d,\"skillLevelCases\":%d,\"completeCases\":%d}}\n",
  validity,thresholds,count-validity-thresholds,count);
}
'''
source = STAGE / 'death-sentry-ai-source-probe.cpp'
source.write_text(cpp)
binary = STAGE / 'death-sentry-ai-source-probe'
subprocess.run(['g++', '-std=c++17', '-Wall', '-Wextra', '-Werror', '-Wno-unused-parameter', str(source), '-o', str(binary)], check=True)
result = json.loads(subprocess.check_output([str(binary)]))
counts = result['verification']
assert counts == dict(initializationCases=1, validityCases=3072, thresholdCases=2400, skillLevelCases=12, completeCases=5484)
san = STAGE / 'death-sentry-ai-source-probe-san'
subprocess.run(['g++', '-std=c++17', '-g', '-fsanitize=address,undefined', '-fno-omit-frame-pointer', str(source), '-o', str(san)], check=True)
log = STAGE / 'death-sentry-ai-sanitizer.log'
with log.open('wb') as output:
    sanitized = subprocess.check_output([str(san)], stderr=output)
assert json.loads(sanitized) == result and not log.read_bytes()
fixture = STAGE / 'death-sentry-ai-source-probe.json'
fixture.write_text(json.dumps(result['cases'], separators=(',', ':')) + '\n')
evidence = {
    'referenceVersion': '3.3.93847',
    'legacyCommit': '0c0bbd1e6bef9181a3da1c046523af622a925416',
    'sourceHashes': {relative: hashlib.sha256(raw).hexdigest()},
    'routineHashes': routines,
    'fixture': {'path': str(fixture.relative_to(ROOT)), 'sha256': hashlib.sha256(fixture.read_bytes()).hexdigest()},
    'verification': counts,
    'scope': 'Complete unchanged legacy AI104, shared initialization and remaining-shot helper; ASan/UBSan clean. Explicit owner/room, skill/formula, enemy selector, corpse selector/distance, chance and mode consumers.',
    'established': [
        'Corpse explosion takes priority before lightning range/chance; corpse-to-enemy distance is strictly below integer (10+rank-1)/2, and remembered corpse GUID is excluded.',
        'Shot debit precedes either UseSkill; selected corpse GUID is remembered before the void UseSkill service, including the last shot. The owner/room helper is rechecked before debit.',
        'Fallback lightning uses skill324/mode14 and strict enemy distance16 with the separate chance service; corpse explosion uses skill312/mode9. Source idle duration is the current row value0.',
        'Missing skill/nonpositive rank after initialization returns without a replacement mode or idle. Invalid owner/room/town or exhausted shots take the common mode0 route.'
    ],
    'limits': [
        'No current executable equivalence, gameplay actor, learned input, actual corpse search/eligibility/consumption, explosion area damage or retirement/GUID lifetime is established.',
        'Shot formula values are supplied services; the current NPC312 row independently defines5+floor(hardFireBlast/3). Chance is Boolean here; this does not certify RNG distribution.',
        'The second-owner-loss cases are service-boundary controls, not evidence that an owner changes during a synchronous gameplay AI call.'
    ],
    'handlerEnabled': False
}
path = ROOT / 'tools/reference/death-sentry-ai-source-evidence.json'
if args.check:
    assert json.loads(path.read_text()) == evidence
else:
    path.write_text(json.dumps(evidence, indent=2) + '\n')
print(json.dumps(counts), 'complete pinned AI104 and helper; clean sanitizers; services explicit')
