#!/usr/bin/env python3
"""Compose unchanged sequence16 scheduler/player event bridge/do35/selector/builder."""
import argparse,hashlib,json,re,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'
def build(check=False):
 subprocess.run(['python3',str(ROOT/'tools/probe_fists_of_fire_reference.py'),'--check'],check=True,stdout=subprocess.DEVNULL)
 tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());hashes={};blocks={}
 def source(rel):
  raw=(STAGE/'d2moo-pinned-source'/rel).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel);hashes[rel]=hashlib.sha256(raw).hexdigest();return raw.decode()
 def routine(text,name):
  m=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',text,re.M);assert m,name;end=text.index('{',m.start())+1;depth=1
  while depth:depth+=(text[end]=='{')-(text[end]=='}');end+=1
  blocks[name]=text[m.start():end];return blocks[name]
 seq=source('source/D2Common/src/DataTbls/SequenceTbls.cpp');units=source('source/D2Common/src/Units/Units.cpp');sunits=source('source/D2Game/src/UNIT/SUnit.cpp');player=source('source/D2Game/src/PLAYER/PlrModes.cpp');select=source('source/D2Game/src/PLAYER/Player.cpp')
 arrays=[]
 for name in ['gPlayerSequenceDragonClaw_HTH_HT1','gPlayerSequenceDragonClaw_HT2']:
  start=seq.index('D2AnimSeqTxt '+name+'[');end=seq.index('};',start)+2;arrays.append(seq[start:end]);blocks[name]=arrays[-1]
 mapping=seq[seq.index('D2PlayerWeaponSequencesStrc* gPlayerWeaponsSequenceTable[24]'):];mapping=mapping[:mapping.index('};')+2];assert [x.strip() for x in mapping.split('{')[1].split('}')[0].split(',') if x.strip()][16]=='&gPlayerWeaponsSequenceDragonClaw'
 base=(STAGE/'fists-fire-builder-source-probe.cpp').read_text().split('int main(){',1)[0];base=re.sub(r'\bevents\b','stateEvents',base)
 base=base.replace('struct D2GameStrc','struct D2AnimSeqTxt{int wSequence,nMode,nFrame,nDir,nEvent;};struct Anim{unsigned char pFrameFlags[144]={};};\nstruct D2GameStrc',1)
 base=base.replace('void* pInventory=(void*)1;','int dwClassId=6,dwAnimMode=18,nSeqCurrentFramePrecise=0,dwSeqSpeed=256,dwSeqFrameCount=0,wAnimSpeed=256,dwFrameCountPrecise=0;D2AnimSeqTxt* pAnimSeq=nullptr;Anim* pAnimData=nullptr;void* pInventory=(void*)1;',1).replace('int nAuraState=4,','int nWeapSel=3,nAuraState=4,',1)
 old='void EVENT_SetEvent(D2GameStrc*,D2UnitStrc*,int,int,int,int){stateEvents++;}'
 new='struct Event{int type,frame,event,param;};std::vector<Event> scheduled;void EVENT_SetEvent(D2GameStrc*,D2UnitStrc*,int type,int frame,int event,int param){if(type!=EVENTTYPE_REMOVESTATE)scheduled.push_back({type,frame,event,param});stateEvents++;}'
 assert old in base;base=base.replace(old,new).replace('#include <algorithm>','#include <algorithm>\n#include <vector>\n#include <cassert>\n#define __stdcall\n#define D2_ASSERT(x) assert(x)',1)
 base+=r'''
constexpr int PLRMODE_ATTACK2=8,PLRMODE_SPECIAL4=16,ANIMSEQ_EVENT_NONE=0,ANIMSEQ_EVENT_MELEE_ATTACK=1,EVENTTYPE_MODECHANGE=1,EVENTTYPE_ENDANIM=2;
D2SkillStrc usedSkill;int rankNow=1,hitMask=3;std::vector<int> phases,handsSeen,flagsSeen;
D2SkillStrc* UNITS_GetUsedSkill(D2UnitStrc*){return &usedSkill;}
int SKILLS_GetFlags(D2SkillStrc*){return 0;}
void SKILLS_SetFlags(D2SkillStrc*,int){std::abort();}
void sub_6FCBC7E0(D2GameStrc*,D2UnitStrc*){std::abort();}
int sub_6FCBC930(D2GameStrc*,D2UnitStrc*){std::abort();}
bool SUNIT_IsDead(D2UnitStrc*){return false;}
int UNITS_GetFrameBonus(D2UnitStrc*){return 0;}
bool UNITS_CanDualWield(D2UnitStrc*){return true;}
D2SkillsTxt* SKILLS_GetSkillsTxtRecordFromSkill(D2SkillStrc*){return &row;}
void INVENTORY_GetEquippedWeapon(void*,D2UnitStrc** p,int* loc,int* left){*p=&rightWeapon;*loc=4;*left=0;}
D2UnitStrc* INVENTORY_GetCompositItem(void*,int loc){return loc==5?&rightWeapon:handMode==1?nullptr:&leftWeapon;}
'''
 base+='\n'.join(arrays)+'\n'+routine(seq,'DATATBLS_GetSequenceEvent')+'\n'+routine(sunits,'sub_6FCBCE70')+'\n'+routine(units,'UNITS_SetAnimationFrame')+'\n'+routine(select,'sub_6FC7C7B0')
 base+=r'''
void sub_6FD13410(D2GameStrc* g,D2UnitStrc* p){int phase=p->dwSeqFrame>>8;phases.push_back(phase);auto* w=sub_6FC7C7B0(p);handsSeen.push_back(w==&rightWeapon?1:w==&leftWeapon?2:0);hit=(hitMask&(1<<phase))!=0;SKILLS_SrvDo035_FistsOfFire_ClawsOfThunder_BladesOfIce(g,p,259,rankNow);flagsSeen.push_back(bool(p->dwFlags&UNITFLAG_SKSRVDOFUNC));}
'''
 base+=routine(player,'sub_6FC80EE0')+r'''
int main(){bool comma=false;printf("[");for(int dual:{0,1})for(int speed:{0,31,64,104,208,256,341,448,512,768})for(int initial:{0,1,2,3})for(int rank:{1,20})for(int mask:{0,1,2,3}){
 owner={};owner.pInventory=(void*)1;owner.pAnimSeq=dual?gPlayerSequenceDragonClaw_HT2:gPlayerSequenceDragonClaw_HTH_HT1;owner.dwSeqSpeed=speed;owner.dwSeqFrameCount=(dual?16:12)*256;owner.dwFlags=0;game.dwGameFrame=100;scheduled.clear();phases.clear();handsSeen.clear();flagsSeen.clear();row={};handMode=dual?3:1;rankNow=rank;hitMask=mask;list={initial,initial*50,9,259,321};exists=initial>0;enabled=exists;selected=record=allocation=true;drains=allocs=stateEvents=0;saved={};
 sub_6FCBCE70(&game,&owner);auto clock=scheduled;for(auto e:clock)if(e.type==EVENTTYPE_MODECHANGE){game.dwGameFrame=e.frame;sub_6FC80EE0(&game,&owner,e.event,e.param);}
 if(comma)printf(",");comma=true;printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,[",dual,speed,initial,rank,mask,list.count,list.ar,allocs,drains);bool sep=false;for(auto e:clock){if(sep)printf(",");sep=true;printf("[%d,%d,%d,%d]",e.type,e.frame-100,e.event,e.param);}printf("],[");for(size_t i=0;i<phases.size();++i){if(i)printf(",");printf("[%d,%d,%d]",phases[i],handsSeen[i],flagsSeen[i]);}printf("]]");
}printf("]\n");}
'''
 path=STAGE/'assassin-claw-sequence-source-probe.cpp';path.write_text(base);binary=path.with_suffix('');subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-omit-frame-pointer','-fno-pie','-no-pie',str(path),'-o',str(binary)],check=True);data=json.loads(subprocess.check_output([str(binary)]));assert len(data)==640
 for dual,speed,initial,rank,mask,count,ar,allocs,drains,events,phases in data:
  n=(2 if dual else 1) if speed else 0;gain=sum(bool(mask&(1<<i)) for i in range(n));assert(count,ar,allocs,drains)==(min(3,initial+gain),min(3,initial+gain)*50,n,n)
  assert phases==[[i,i+1,1 if not dual or i==1 else 0] for i in range(n)]
  expected=[]
  if speed:
   for i,frame in enumerate([6,10] if dual else [6]):expected.append([1,max(1,(frame*256+speed-1)//speed),1,i])
   expected.append([2,max(1,((16 if dual else 12)*256+speed-1)//speed),0,0])
  else:expected=[[2,1,0,0]]
  assert events==expected,(dual,speed,events,expected)
 fixture=STAGE/'assassin-claw-sequence-source-probe.json';fixture.write_text(json.dumps(data,separators=(',',':'))+'\n')
 evidence=dict(referenceVersion='3.3.93847',talentId='assassin.fistsoffire',legacyCommit=tree['sha'],sourceHashes=hashes,compiledRoutines={k:hashlib.sha256(v.encode()).hexdigest() for k,v in blocks.items()},reuse='tools/reference/fists-fire-source-evidence.json',fixture=dict(path=str(fixture.relative_to(ROOT)),sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(),cases=len(data),sanitizers='ASan+UBSan'),established=['Complete scheduler numbers melee action contacts0,1 independently of visual sequence frame6,10. Complete player callback writes that ordinal with UNITS_SetAnimationFrame before invoking the declared skill-dispatch bridge.','Complete player weapon selector therefore chooses first then second hand for weapsel3. Full do35 clears its flag after first dual contact to allow the second callback, then sets it; single-claw flags complete at first contact. Both events use complete do34 hit/gain logic.','Legacy sequence16 has12points/action6 for single and16points/actions6,10 for dual. Supplied rates compose exact event/end schedules, including two contacts within a fast tick and zero-rate no-contact control.'],declaredServices=['Frame bonus0 and sequence rate are explicit inputs, not compiled animation-rate derivation. Current A2/S4 selection/rate/executable acceptance remains separate.','Skill-dispatch bridge directly invokes full do35, supplying hit masks/rank; generic dispatcher/start/payment is not compiled here. Player used-skill flags0 and living state select ordinary callback path; unselected branches abort.','Inventory mapping and stat-list/damage allocation services are reused from bounded builder proof. No full native physical/proc/geometry/modern controller certification.'],limits=['Native learned Fists handler, current SQ rate calculation and full area/field/conversion/lifecycle/input are pending.','Current local animation acquisition corroborates cached metadata only; these hardcoded legacy player sequence arrays are separate source evidence.','The event ordinal bridge is established, not a guessed replacement of visual frame with hand index.'])
 out=ROOT/'tools/reference/assassin-claw-sequence-source-evidence.json';encoded=json.dumps(evidence,indent=2)+'\n'
 if check:assert out.read_text()==encoded,'Claw sequence evidence stale'
 else:out.write_text(encoded)
 print('Assassin claw sequence:640 complete scheduler/player callback/ordinal selector/do35/do34 compositions pass; rate derivation/native handler separate.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
