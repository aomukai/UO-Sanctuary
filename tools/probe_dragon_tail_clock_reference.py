#!/usr/bin/env python3
"""Compile full source server event scheduler and frame-bonus getter for current KK metadata."""
import argparse,hashlib,json,re,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'
def build(check=False):
 tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());blocks={};hashes={}
 for rel,names in {'source/D2Game/src/UNIT/SUnit.cpp':['sub_6FCBCE70'],'source/D2Common/src/Units/Units.cpp':['UNITS_GetFrameBonus']}.items():
  raw=(STAGE/'d2moo-pinned-source'/rel).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel);hashes[rel]=hashlib.sha256(raw).hexdigest();text=raw.decode()
  for name in names:
   m=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',text,re.M);assert m;end=text.index('{',m.start())+1;depth=1
   while depth:depth+=(text[end]=='{')-(text[end]=='}');end+=1
   blocks[name]=text[m.start():end]
 metadata=json.loads((ROOT/'tools/reference/d2r-local-animation-evidence.json').read_text());kk=[x for x in metadata['records'] if x['code'].startswith('AIKK')];assert len(kk)==10 and all(x['frames']==13 and x['speed']==256 and x['events']==[[4,1]] for x in kk)
 cpp=r'''
#include <cassert>
#include <cstdint>
#include <cstdio>
#include <cstdlib>
#include <vector>
#define __fastcall
#define __stdcall
#define D2_ASSERT(x) assert(x)
constexpr int NUM_WEAPON_CLASSES=14,NUMBER_OF_PLAYERCLASSES=7,UNIT_TYPES_COUNT=6,UNIT_PLAYER=0,WEAPONCLASS_HTH=0,PLRMODE_ATTACK1=7,PLRMODE_ATTACK2=8,PLRMODE_SPECIAL3=15,PLRMODE_SPECIAL4=16,PLRMODE_KICK=12,EVENTTYPE_MODECHANGE=0,EVENTTYPE_ENDANIM=1;
struct Anim{int pFrameFlags[144]={};};struct D2AnimSeqTxt{};
struct D2UnitStrc{int dwClassId=6,dwUnitType=0,dwAnimMode=PLRMODE_KICK;void* pAnimSeq=nullptr;int dwSeqSpeed=0,dwSeqFrameCount=0,wAnimSpeed=0,dwFrameCountPrecise=13<<8,nSeqCurrentFramePrecise=0;Anim* pAnimData;};
struct D2GameStrc{int dwGameFrame=100;};struct Event{int kind,frame,arg,param;};std::vector<Event> events;
void D2COMMON_11013_ConvertMode(D2UnitStrc*,int*,int*,int*,const char*,int){}
D2UnitStrc* D2Common_10434(D2UnitStrc*,int){std::abort();}
int ITEMS_GetWeaponClassId(D2UnitStrc*){std::abort();}bool UNITS_CanDualWield(D2UnitStrc*){std::abort();}
void DATATBLS_GetSequenceEvent(D2AnimSeqTxt*,int,int*){std::abort();}
void EVENT_SetEvent(D2GameStrc*,D2UnitStrc*,int kind,int frame,int arg,int param){events.push_back({kind,frame,arg,param});}
'''
 cpp+='\n'+blocks['UNITS_GetFrameBonus']+'\n'+blocks['sub_6FCBCE70']+r'''
int main(){bool comma=false;printf("[");for(int weapon=0;weapon<10;weapon++)for(int speed=0;speed<=448;speed++){
 Anim anim;anim.pFrameFlags[4]=1;D2UnitStrc unit;unit.pAnimData=&anim;unit.wAnimSpeed=speed;D2GameStrc game;events.clear();sub_6FCBCE70(&game,&unit);int action=0,end=0,count=0,param=-1;
 for(auto e:events){if(e.kind==EVENTTYPE_MODECHANGE){action=e.frame-game.dwGameFrame;param=e.param;count++;if(e.arg!=1)std::abort();}else end=e.frame-game.dwGameFrame;}
 if(comma)printf(",");comma=true;printf("[%d,%d,%d,%d,%d,%d,%d]",weapon,speed,action,end,count,param,unit.nSeqCurrentFramePrecise);
}printf("]\n");}
'''
 p=STAGE/'dragon-tail-clock-source-probe.cpp';p.write_text(cpp);binary=p.with_suffix('');subprocess.run(['g++','-std=c++20','-O1','-fsanitize=address,undefined','-fno-pie','-no-pie',str(p),'-o',str(binary)],check=True);rows=json.loads(subprocess.check_output([str(binary)]));assert len(rows)==4490
 for weapon,speed,action,end,count,param,origin in rows:
  assert(action,end,count,param,origin)==((1024+speed-1)//speed,(3328+speed-1)//speed,1,0,25600) if speed else (action,end,count,param,origin)==(0,1,0,-1,0)
 fixture=STAGE/'dragon-tail-clock-source-probe.json';fixture.write_text(json.dumps(rows,separators=(',',':'))+'\n')
 e=dict(referenceVersion='3.3.93847',talentId='assassin.dragontail',handlerEnabled=False,legacyCommit=tree['sha'],sourceHashes=hashes,compiledRoutines={k:hashlib.sha256(v.encode()).hexdigest() for k,v in blocks.items()},localAnimationEvidence='tools/reference/d2r-local-animation-evidence.json',fixture=dict(path=str(fixture.relative_to(ROOT)),sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(),cppSha256=hashlib.sha256(p.read_bytes()).hexdigest(),cases=len(rows),sanitizers='ASan+UBSan'),established=['Complete source frame-bonus getter returns0for player KK mode without weapon selection. Complete event scheduler scans each marker index once,so low animation advance does not repeat the action.','For current13frame/action4KK metadata and positive advance1..448,the single action is scheduled ceil(1024/advance)frames after start and end ceil(3328/advance)frames after start. Zero speed schedules only end at+1. The source stores the original game tick in nSeqCurrentFramePrecise.'],services=['Mode conversion is identity for the supplied normal Assassin player/Kick context. Non-KK weapon-selection and sequence services abort rather than inventing those branches. Event allocation records ordered requests;the full event queue,mode handler/start27/posting/interrupt/reentry and native input are not compiled.','All10actual local KK records share the same metadata. Their supplied frame flags feed unchanged source scheduling;this does not prove the current executable scheduler remained unchanged.'],limits=['Need complete current start27rate posting/lifetime and native action/paid cancellation,input and save/reload integration.','Modern charged always-hit/block/one-charge consumption and full kick/physical-dependent fire packet/stat/proc ordering remain separate gates;no learned handler enabled.'])
 out=ROOT/'tools/reference/dragon-tail-clock-source-evidence.json';encoded=json.dumps(e,indent=2)+'\n'
 if check:assert out.read_text()==encoded,'Dragon Tail clock evidence stale'
 else:out.write_text(encoded)
 print('Dragon Tail clock:4490 complete server-scheduler/frame-bonus contexts pass;single KK action/end requests;native handler remains gated.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
