#!/usr/bin/env python3
"""Execute the complete player mode-entry gate for Strafe's interrupt flag."""
from pathlib import Path
import argparse,hashlib,json,re,subprocess
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills';p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');args=p.parse_args()
tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());hashes={};blocks={}
def routine(rel,name):
 raw=(STAGE/'d2moo-pinned-source'/rel).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(i['sha'] for i in tree['tree'] if i['path']==rel);hashes[rel]=hashlib.sha256(raw).hexdigest();text=raw.decode();m=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',text,re.M);assert m,name;e=text.index('{',m.start())+1;depth=1
 while depth:depth+=(text[e]=='{')-(text[e]=='}');e+=1
 body=text[m.start():e];blocks[name]=hashlib.sha256(body.encode()).hexdigest();return body
body=routine('source/D2Game/src/PLAYER/PlrModes.cpp','D2GAME_PLRMODES_First_6FC7F340')
headerPath='source/D2Common/include/Units/Player.h';header=(STAGE/'d2moo-pinned-source'/headerPath).read_bytes();assert hashlib.sha1(b'blob '+str(len(header)).encode()+b'\0'+header).hexdigest()==next(i['sha'] for i in tree['tree'] if i['path']==headerPath);hashes[headerPath]=hashlib.sha256(header).hexdigest()
enum=header.decode().split('enum D2C_PlayerModes',1)[1].split('};',1)[0];modeValues={name:i for i,name in enumerate(re.findall(r'\bPLRMODE_\w+',enum))}
assert {k:modeValues[k] for k in ['PLRMODE_DEATH','PLRMODE_NEUTRAL','PLRMODE_WALK','PLRMODE_RUN','PLRMODE_GETHIT','PLRMODE_ATTACK1','PLRMODE_ATTACK2','PLRMODE_CAST','PLRMODE_THROW','PLRMODE_SPECIAL1','PLRMODE_DEAD','PLRMODE_SEQUENCE','PLRMODE_KNOCKBACK']}==dict(PLRMODE_DEATH=0,PLRMODE_NEUTRAL=1,PLRMODE_WALK=2,PLRMODE_RUN=3,PLRMODE_GETHIT=4,PLRMODE_ATTACK1=7,PLRMODE_ATTACK2=8,PLRMODE_CAST=10,PLRMODE_THROW=11,PLRMODE_SPECIAL1=13,PLRMODE_DEAD=17,PLRMODE_SEQUENCE=18,PLRMODE_KNOCKBACK=19)

cpp=r'''#include <cstdint>
#include <cstdio>
#include <initializer_list>
#define __fastcall
constexpr int PLRMODE_DEATH=0,PLRMODE_NEUTRAL=1,PLRMODE_WALK=2,PLRMODE_RUN=3,PLRMODE_GETHIT=4,PLRMODE_ATTACK1=7,PLRMODE_ATTACK2=8,PLRMODE_CAST=10,PLRMODE_THROW=11,PLRMODE_SPECIAL1=13,PLRMODE_DEAD=17,PLRMODE_SEQUENCE=18,PLRMODE_KNOCKBACK=19;
constexpr int STATE_UNINTERRUPTABLE=0,STATE_CONCENTRATION=1,STATE_CONCENTRATE=2,SKILLSFLAGINDEX_INTERRUPT=0,EVENTTYPE_ENDANIM=1,STAT_SKILL_CONCENTRATION=0;
unsigned gdwBitMasks[32];struct D2GameStrc{int dwGameFrame=100;};struct D2SkillStrc{};struct D2UnitStrc{int dwAnimMode=7,pSeed=0;};struct D2SkillsTxt{unsigned dwFlags[2]={};int wSrvDoFunc=12;};
D2SkillsTxt row;D2SkillStrc used;bool hasRow=true,hasSkill=true,uninterrupt=false;int neutral=0,end=100;
int STATES_CheckState(D2UnitStrc*,int s){return s==0&&uninterrupt;}D2SkillStrc* UNITS_GetUsedSkill(D2UnitStrc*){return hasSkill?&used:nullptr;}int SKILLS_GetSkillIdFromSkill(D2SkillStrc*,const char*,int){return 26;}
D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int){return hasRow?&row:nullptr;}void sub_6FC817D0(D2GameStrc*,D2UnitStrc*,void*,int,int,int,int){neutral++;}
int EVENT_GetEventFrame(D2GameStrc*,D2UnitStrc*,int){return end;}int ITEMS_RollRandomNumber(int*){return 0;}void* STATLIST_GetStatListFromUnitAndState(D2UnitStrc*,int){return nullptr;}int STATLIST_GetStatValue(void*,int,int){return 0;}
'''+body+r'''
int main(){gdwBitMasks[0]=1;D2GameStrc game;D2UnitStrc owner;bool first=true;std::printf("[");
for(int record:{0,1})for(int skill:{0,1})for(int interrupt:{0,1})for(int current:{0,1,7,17})for(int next:{0,2,3,4,7,8,10,11,13,18,19})for(int control:{0,1})for(int elapsed:{0,5,6})for(int state:{0,1}){
 hasRow=record;hasSkill=skill;row.dwFlags[0]=interrupt;owner.dwAnimMode=current;game.dwGameFrame=100+elapsed;uninterrupt=state;neutral=0;int result=D2GAME_PLRMODES_First_6FC7F340(&game,&owner,next,control,&used);
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",record,skill,interrupt,current,next,control,elapsed,state,result,neutral);
}std::printf("]\n");}
'''
path=STAGE/'strafe-mode-source-probe.cpp';path.write_text(cpp);binary=STAGE/'strafe-mode-source-probe';subprocess.run(['g++','-std=c++20','-O2',str(path),'-o',str(binary)],check=True)
data=json.loads(subprocess.check_output([str(binary)]));assert len(data)==4224
for record,skill,interrupt,current,nxt,control,elapsed,state,result,neutral in data:
 if state or current in [0,17]:expected=0
 elif nxt==0 or not control or not skill or not record:expected=1
 elif not interrupt:expected=int(current==1 or nxt in [7,8,10,11,13,18] and elapsed<=5)
 else:expected=1
 assert result==expected
 assert neutral==int(bool(expected and current==1 and record and skill and not interrupt and nxt!=0 and control))
fixture=STAGE/'strafe-mode-source-probe.json';fixture.write_text(json.dumps(data,separators=(',',':'))+'\n')
e=dict(referenceVersion='3.3.93847',talentId='amazon.strafe',handlerEnabled=False,legacyCommit=tree['sha'],sourceHashes=hashes,compiledRoutines=list(blocks),blockHashes=blocks,probe=dict(cases=len(data),path=str(fixture.relative_to(ROOT)),sha256=hashlib.sha256(fixture.read_bytes()).hexdigest()),established=['Complete mode-entry gate with Strafe do12/no interrupt flag refuses ordinary checked walk/run/get-hit/knockback entry while active. Death entry is allowed unless already in death/dead or explicit uninterruptable state. Missing row/skill and forced caller controls are distinct branches.','Attack/cast/throw/special1/sequence requests can pass this first gate through end-event-plus5-frame comparison; this gate alone does not establish whether subsequent input is queued, replaces/restarts the sequence or is refused by outer callers. Do not equate its accepted return with a completed mode change.'],limits=['State/used-skill/table/end-event services and player neutral transition are declared. The complete mode-change/input/event callers, current modern queuing/cancellation and force/death ordering remain separate; no native movement/attack lock is certified.','This is supplemental older engine evidence. Current pinned Strafe interrupt field is absent, but exact3.3.93847 gameplay, gear/dodge/owner/scheduler/rendered-client behavior still requires native/current acceptance.'])
out=ROOT/'tools/reference/strafe-mode-engine-evidence.json';encoded=json.dumps(e,indent=2)+'\n'
if args.check:assert out.read_text()==encoded,'Strafe mode evidence stale'
else:out.write_text(encoded)
print('Strafe:4224 complete player-mode entry cases; outer queued/restarted attack behavior remains unproven.')
