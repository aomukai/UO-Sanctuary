#!/usr/bin/env python3
"""Compose full older original Fury callback,sequence,do48 and event rewind."""
import hashlib,json,subprocess
from pathlib import Path
from probe_current_item_affixes import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'shadow-fury-callback-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
 STAGE.mkdir(exist_ok=True);tree=json.loads((BASE/'d2moo-source-tree.json').read_text());inputs={};routines={}
 def source(rel,name):
  p=BASE/'d2moo-pinned-source'/rel;b=p.read_bytes();assert hashlib.sha1(b'blob '+str(len(b)).encode()+b'\0'+b).hexdigest()==next(r['sha']for r in tree['tree']if r['path']==rel);inputs[str(p.relative_to(ROOT))]=sha(p);s=function(b.decode(),name);routines[name]=hashlib.sha256(s.encode()).hexdigest();return s
 old=BASE/'blade-fury-resource-source-probe.cpp';cpp=old.read_text().split('int32_t __fastcall SKILLS_GetProgressiveSkillMissileId',1)[0];inputs[str(old.relative_to(ROOT))]=sha(old)
 cpp=cpp.replace('#include <iterator>','#include <iterator>\n#include <vector>\n#include <cstdlib>\n#define __stdcall\n#define D2_ASSERT(x) assert(x)\nusing D2AnimSeqEvent=int;\nstruct D2AnimSeqTxt{int wSequence,nMode,nFrame,nDir,nEvent;};struct Anim{unsigned char pFrameFlags[144]={};};')
 cpp=cpp.replace('dwUnitType=0;unsigned dwFlags=64,dwSeqFrame=0;','dwUnitType=1;int dwClassId=417,dwAnimMode=14,nSeqCurrentFramePrecise=0,dwSeqSpeed=256,dwSeqFrameCount=17*256,wAnimSpeed=256,dwFrameCountPrecise=17*256,nActionFrame=0;unsigned dwFlags=0,dwSeqFrame=0,dwSeqMode=0;D2AnimSeqTxt*pAnimSeq=nullptr;Anim*pAnimData=nullptr;')
 cpp=cpp.replace('int param=0,nCharges=0;','int param=0,nCharges=0;unsigned flags=0;').replace('type==0&&id==7','type==1&&id==7').replace('bool record=true,learned=true,active=false,','bool lease=false;bool record=true,learned=true,active=false,').replace('return active?&state:nullptr;','return lease?&state:nullptr;').replace('state.until=until;return allocation?&state:nullptr;','state.until=until;lease=allocation;return allocation?&state:nullptr;')
 cpp=cpp.replace('void sub_6FCBD120(D2GameStrc*,D2UnitStrc*,int one){assert(one==1);rewinds++;}','void sub_6FCBD120(D2GameStrc*,D2UnitStrc*,int);')
 cpp=cpp.replace('void EVENT_SetEvent(D2GameStrc*,D2UnitStrc*,int type,int until,int,int){assert(type==EVENTTYPE_REMOVESTATE&&until==state.until);}',r'''
constexpr int EVENTTYPE_MODECHANGE=2,EVENTTYPE_ENDANIM=3,UNITFLAG_SQGFXCHANGE=0x4000; // symbolic event identities,not wire values.
struct Event{int type,frame,kind;};std::vector<Event> events;bool infinite=true,ended=false;int endFrame=0;
void EVENT_SetEvent(D2GameStrc*,D2UnitStrc*,int type,int frame,int kind,int){events.push_back({type,frame,kind});}
void D2GAME_EVENTS_Delete_6FC34840(D2GameStrc*,D2UnitStrc*,int type,int){if(type==EVENTTYPE_MODECHANGE){infinite=false;}std::erase_if(events,[&](auto e){return e.type==type;});}
''')
 cpp=cpp.replace('int rank=3;', 'std::vector<int> shots;constexpr int ANIMSEQ_EVENT_NONE=0;int rank=3;')
 cpp=cpp.replace('emits++;order+=\'E\';','shots.push_back(game.dwGameFrame);emits++;order+=\'E\';')
 unit='source/D2Common/src/Units/Units.cpp';seq='source/D2Common/src/DataTbls/SequenceTbls.cpp';skill='source/D2Game/src/SKILLS/SkillAss.cpp'
 data=json.loads((ROOT/'data/Sanctuary/Balance/shadow-fury-sequence.json').read_text());assert data['Mode']==14 and data['Points']==17;inputs['data/Sanctuary/Balance/shadow-fury-sequence.json']=sha(ROOT/'data/Sanctuary/Balance/shadow-fury-sequence.json')
 cpp+='\nD2AnimSeqTxt rows[17]={'+','.join('{0,'+','.join(map(str,r))+'}'for r in data['Rows'])+'};\n'
 cpp+='D2AnimSeqTxt* DATATBLS_GetMonSeqTxtRecordFromUnit(D2UnitStrc*){return rows;}int DATATBLS_GetSeqFramePointsCount(D2UnitStrc*){return 17*256;}int DATATBLS_GetSeqFrameCount(D2UnitStrc*){return 17;}\nvoid UNITS_SetAnimActionFrame(D2UnitStrc*,int){std::abort();}int UNITS_GetFrameBonus(D2UnitStrc*){return 0;}\n'
 cpp+=source(seq,'DATATBLS_ComputeSequenceAnimation')+'\n'+source(seq,'DATATBLS_GetSequenceEvent')+'\n'
 for n in ['UNITS_InitializeSequence','UNITS_StopSequence','UNITS_IsAtEndOfFrameCycle']:cpp+=source(unit,n)+'\n'
 cpp+=source('source/D2Game/src/UNIT/SUnit.cpp','sub_6FCBD120')+'\n'
 cpp+='\nint SKILLS_GetProgressiveSkillMissileId(D2UnitStrc*,int){return 506;}int SKILLS_EvaluateProgressiveSkillCalc(D2UnitStrc*,int,int){return 5;}int32_t D2GAME_SKILLMANA_Consume_6FD10A50(D2GameStrc*,D2UnitStrc*,int,int){payCalls++;return 0;}\n'
 for n in ['SKILLS_SrvDo048_BladeFury','SKILLS_SrvSt26_BladeFury','SKILLS_StatRemoveCallback_RemoveState']:cpp+=source(skill,n)+'\n'
 cpp+='\nD2SkillStrc*UNITS_GetUsedSkill(D2UnitStrc*){return &::skill;}unsigned SKILLS_GetFlags(D2SkillStrc*p){return p->flags;}void SKILLS_SetFlags(D2SkillStrc*p,unsigned f){p->flags=f;}void sub_6FCBC7E0(D2GameStrc*,D2UnitStrc*){std::abort();}int sub_6FCBC930(D2GameStrc*,D2UnitStrc*){std::abort();}void sub_6FC63680(D2GameStrc*,D2UnitStrc*){ended=true;endFrame=game.dwGameFrame;}\nvoid sub_6FD13410(D2GameStrc*g,D2UnitStrc*p){SKILLS_SrvDo048_BladeFury(g,p,266,rank);}\n'
 cpp+=source('source/D2Game/src/MONSTER/MonsterMode.cpp','sub_6FC64E90')
 # Infinite MODECHANGE is initially installed by sub6FCBD3A0. The engine
 # executes infinite monster timers before finite timers;rewind deletes that
 # same infinite entry. End handling/state removal are declared services.
 cpp+=r'''
int main(){printf("[");bool sep=false;for(int epoch:{1,1000,100000})for(int goodAim:{0,1})for(int goodFactory:{0,1}){
owner={};skill={};row={};row.wStartMana=0; // approved NPC threshold exception only.
lease=active=false;allocation=true;aim=goodAim;factory=goodFactory;infinite=true;ended=false;emits=made=allocs=payCalls=endFrame=0;events.clear();shots.clear();game.dwGameFrame=epoch;UNITS_InitializeSequence(&owner);assert(SKILLS_SrvSt26_BladeFury(&game,&owner,266,rank)==1);
for(int frame=epoch+1;frame<=epoch+60;frame++){game.dwGameFrame=frame;if(infinite&&!ended){sub_6FC64E90(&game,&owner);}for(size_t i=0;i<events.size();){auto e=events[i];if(e.frame!=frame){i++;continue;}events.erase(events.begin()+i);if(e.type==EVENTTYPE_MODECHANGE&&!ended){sub_6FC64E90(&game,&owner);i=0;}else if(e.type==EVENTTYPE_ENDANIM&&!ended){ended=true;endFrame=frame;infinite=false;}else if(e.type==EVENTTYPE_REMOVESTATE){lease=false;SKILLS_StatRemoveCallback_RemoveState(&owner,STATE_INFERNO,&state);}}}
if(sep)printf(",");sep=true;printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%u,[",epoch,goodAim,goodFactory,endFrame-epoch,owner.dwSeqFrame,owner.dwFrameCountPrecise,skill.param-epoch,lease,active,allocs,owner.dwFlags);for(size_t i=0;i<shots.size();i++)printf("%s%d",i?",":"",shots[i]-epoch);printf("]]");}printf("]\n");}
'''
 generated=STAGE/'source.cpp';generated.write_text(cpp);binary=STAGE/'probe'
 with(STAGE/'compiler.log').open('w')as log:subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all','-fno-pie','-no-pie',str(generated),'-o',str(binary)],stdout=log,stderr=subprocess.STDOUT,check=True)
 r=subprocess.run([str(binary)],capture_output=True);(STAGE/'sanitizer.log').write_bytes(r.stderr);assert r.returncode==0 and not r.stderr,r.stderr.decode();cases=json.loads(r.stdout);assert len(cases)==12
 fixture=STAGE/'cases.json';fixture.write_text(json.dumps(cases,separators=(',',':'))+'\n')
 receipt=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],contexts=12,completeRoutines=routines,inputs=inputs,artifacts={str(p.relative_to(ROOT)):sha(p)for p in [generated,binary,fixture,STAGE/'compiler.log',STAGE/'sanitizer.log',Path(__file__).resolve()]},scope='Composed complete old original mode14 callback/sequence/do48/start26/remove-state/rewind bodies. Declared current17point row,free NPC threshold0,missile/aim/factory services,neutral/end and bounded symbolic event-list host. Actual old infinite-before-finite order and deletion semantics reviewed;not a full Game/Event executable. Critical and allocation services are not real. Tests deliberately vary game epoch to expose pose-versus-epoch rewind dependence,not assert modern D2R parity. Source event queue,skillflags1 and generic resource wrapper remain separately scoped.')
 (ROOT/'tools/reference/shadow-fury-callback-source-evidence.json').write_text(json.dumps(receipt,indent=2)+'\n');print(json.dumps(cases));print('Own Fury callback:12new composed older-source contexts;ASan/UBSan clean;modern release/rewind behavior remains uncertain.')
if __name__=='__main__':main()
