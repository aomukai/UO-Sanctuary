#!/usr/bin/env python3
"""Execute complete monster mode14 entry/callback against the real sequence code.

Start/do, target validation and end/neutral scheduling are explicit services.
This prepares the world-actor controller without enabling an unfinished skill.
"""
from pathlib import Path
import argparse,hashlib,json,re,subprocess,sys
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'
p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');args=p.parse_args()
subprocess.run([sys.executable,str(ROOT/'tools/probe_sentry_sequence_reference.py'),'--check'],check=True,stdout=subprocess.DEVNULL)
tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());rel='source/D2Game/src/MONSTER/MonsterMode.cpp';raw=(STAGE/'d2moo-pinned-source'/rel).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel)
s=raw.decode();routines={}
def extract(name):
 m=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',s,re.M);assert m,name;end=s.index('{',m.start())+1;depth=1
 while depth:depth+=(s[end]=='{')-(s[end]=='}');end+=1
 body=s[m.start():end];routines[name]=hashlib.sha256(body.encode()).hexdigest();return body
cpp=(STAGE/'sentry-sequence-source-probe.cpp').read_text().split('int main()',1)[0]
cpp=cpp.replace('struct D2UnitStrc{','struct D2UnitStrc{int dwAnimMode=14;')
cpp+=r'''
struct D2GameStrc{};struct D2ModeChangeStrc{D2UnitStrc* pUnit;};struct D2SkillStrc{int flags=0;}skill;
int targetStatus=2,starts=0,dispatches=0,ends=0,updates=0;bool startSuccess=true,markDo=true;
void SUNIT_SetCombatMode(D2GameStrc*,D2UnitStrc* unit,int mode){unit->dwAnimMode=mode;UNITS_InitializeSequence(unit);}
int sub_6FD12FD0(D2GameStrc*,D2UnitStrc*){starts++;return startSuccess;}
void sub_6FC63680(D2GameStrc*,D2UnitStrc*){ends++;}
D2SkillStrc* UNITS_GetUsedSkill(D2UnitStrc*){return &skill;}
int SKILLS_GetFlags(D2SkillStrc* p){return p->flags;}
void SKILLS_SetFlags(D2SkillStrc* p,int flags){p->flags=flags;}
void sub_6FCBC7E0(D2GameStrc*,D2UnitStrc*){updates++;}
int sub_6FCBC930(D2GameStrc*,D2UnitStrc*){return targetStatus;}
void sub_6FD13410(D2GameStrc*,D2UnitStrc* unit){dispatches++;if(markDo)unit->dwFlags|=64;}
'''
for name in ['sub_6FC64E60','sub_6FC64E90']:cpp+='\n'+extract(name)+'\n'
cpp+=r'''
int main(){for(int i=0;i<15;i++)rows[i]={9u,unsigned(i),0,i==2?1:0};D2GameStrc game;
 for(int success:{0,1})for(int flagged:{0,1}){
 D2UnitStrc unit;unit.dwFlags=flagged?64:0;D2ModeChangeStrc change{&unit};starts=0;startSuccess=success;int result=sub_6FC64E60(&game,&change);
 assert(result==success&&starts==1&&unit.dwAnimMode==14&&!(unit.dwFlags&64)&&unit.dwSeqSpeed==256&&unit.dwFrameCountPrecise==3840);
 }
 printf("[");bool first=true;
 for(int action:{0,1,2,4})for(int flags:{0,1,2,3})for(int target:{1,2})for(int flagged:{0,1})for(int mark:{0,1})for(int remaining:{0,1,256,3840}){
 D2UnitStrc unit;UNITS_InitializeSequence(&unit);unit.nActionFrame=action;unit.dwFrameCountPrecise=remaining;unit.dwFlags=flagged?64:0;skill.flags=flags;targetStatus=target;markDo=mark;dispatches=ends=updates=0;
 int result=sub_6FC64E90(&game,&unit);
 printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%u,%d]",first?"":",",action,flags,target,flagged,mark,remaining,result,dispatches,ends,updates,skill.flags,unit.dwFlags,unit.dwFrameCountPrecise);first=false;
 }printf("]\n");
}
'''
source=STAGE/'sentry-mode-source-probe.cpp';source.write_text(cpp);binary=source.with_suffix('')
subprocess.run(['g++','-std=c++17','-O2',str(source),'-o',str(binary)],check=True);data=json.loads(subprocess.check_output([str(binary)]));assert len(data)==512
for action,flags,target,flagged,mark,remaining,result,calls,ended,updates,outflags,outunit,left in data:
 expected=0 if not remaining else int(bool(flags&1) and target==2)+int(action==4 or not(flags&1 and target==2) and not flagged and action in [1,2])
 assert calls==expected and result==(2 if not remaining else 1) and ended==int(not remaining)
 assert updates==int(remaining>0 and bool(flags&1)) and left==(0 if not remaining else remaining-256)
san=STAGE/'sentry-mode-source-probe-san';subprocess.run(['g++','-std=c++17','-g','-fsanitize=address,undefined','-fno-omit-frame-pointer',str(source),'-o',str(san)],check=True)
with (STAGE/'sentry-mode-sanitizer.log').open('wb') as log:sanitized=subprocess.check_output([str(san)],stderr=log)
assert json.loads(sanitized)==data and not (STAGE/'sentry-mode-sanitizer.log').read_bytes()
fixture=STAGE/'sentry-mode-source-probe.json';fixture.write_text(json.dumps(data,separators=(',',':'))+'\n')
e={'referenceVersion':'3.3.93847','legacyCommit':tree['sha'],'sourceHashes':{rel:hashlib.sha256(raw).hexdigest()},'routineHashes':routines,'sequenceEvidence':'tools/reference/sentry-sequence-source-evidence.json','verification':{'entryCases':4,'completeModeCallbackCases':len(data)},'fixture':{'path':str(fixture.relative_to(ROOT)),'sha256':hashlib.sha256(fixture.read_bytes()).hexdigest()},'established':['Mode14 entry initializes the sequence and clears server-do flag64 before generic start, including a failed start result.','Complete mode14 callback tests end before action; unflagged normal skill dispatches action1/2 once after a do sets flag64, and advances sequence after dispatch. Channel flag1 and action4 controls remain distinct.'],'limits':['Actual mode-change event queue, start/do implementations, owner/target, base animation rate and end/neutral/AI scheduling are declared services, not a live trap controller.','Current pinned sentry entries contain event1 only; event2/4 and channelled flags are generic routine controls, not asserted Charged Bolt Sentry behavior.'],'handlerEnabled':False}
path=ROOT/'tools/reference/sentry-mode-source-evidence.json'
if args.check:assert json.loads(path.read_text())==e
else:path.write_text(json.dumps(e,indent=2)+'\n')
print(len(data),'complete mode14 callback cases plus4 entry cases; sanitizers clean; world/event integration pending')
