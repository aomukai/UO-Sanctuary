#!/usr/bin/env python3
"""Complete ordinary-animation percent rewind with the owned kick header byte."""
import hashlib,json,subprocess
from pathlib import Path
from probe_current_item_affixes import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'shadow-kick-rewind-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
 STAGE.mkdir(parents=True,exist_ok=True);tree=json.loads((BASE/'d2moo-source-tree.json').read_text());inputs={}
 def pin(rel,blob=False):
  p=ROOT/rel;raw=p.read_bytes();inputs[rel]=sha(p)
  if blob:
   source=rel.split('d2moo-pinned-source/',1)[1]
   assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha']for x in tree['tree']if x['path']==source)
  return raw
 source=pin('.local/class-skills/d2moo-pinned-source/source/D2Game/src/UNIT/SUnit.cpp',True).decode();body=function(source,'sub_6FCBCFD0')
 header=pin('.local/class-skills/d2moo-pinned-source/source/D2Common/include/DataTbls/AnimTbls.h',True).decode()
 assert 'uint8_t pFrameFlags[MAX_FRAME_FLAGS];'in header and '//0x10'in header and 'int32_t dwAnimSpeed;'in header
 evidence=json.loads(pin('tools/reference/d2r-local-animation-evidence.json'));raw=pin(evidence['localRawPath']);assert hashlib.sha256(raw).hexdigest()==evidence['sha256']
 offset=0;rows=[]
 for _ in range(256):
  n=int.from_bytes(raw[offset:offset+4],'little');offset+=4
  for _ in range(n):
   row=raw[offset:offset+160];offset+=160;code=row[:8].split(b'\0')[0].decode('ascii')
   if code in ['AIKKHT1','AIKKHT2','AIKKHTH']:
    assert int.from_bytes(row[8:12],'little')==13 and int.from_bytes(row[12:16],'little')==256 and row[15]==0
    assert [(i,v)for i,v in enumerate(row[16:])if v]==[(4,1)]
    rows.append(dict(code=code,rawSha256=hashlib.sha256(row).hexdigest(),precedingFlag=row[15]))
 assert len(rows)==3 and offset==len(raw)
 cpp=r'''#include <array>
#include <cassert>
#include <cstdint>
#include <cstdio>
#include <cstdlib>
#include <vector>
#define __fastcall
constexpr int EVENTTYPE_MODECHANGE=1,EVENTTYPE_ENDANIM=2;
struct D2AnimSeqTxt{};
// Pointer into one byte array retains a valid object-representation byte
// at index-1. Its value is supplied from the owned record's speed header.
struct Anim{std::array<uint8_t,160>raw{};uint8_t*pFrameFlags=raw.data()+16;};
struct D2GameStrc{int dwGameFrame;};
struct D2UnitStrc{int nSeqCurrentFramePrecise;void*pAnimSeq=nullptr;int dwSeqSpeed=0,dwSeqFrameCount=0,wAnimSpeed=0,dwFrameCountPrecise=13*256;Anim*pAnimData;};
std::vector<std::array<int,3>>events;std::vector<int>deletes;
void D2GAME_EVENTS_Delete_6FC34840(D2GameStrc*,D2UnitStrc*,int type,int mode){assert(mode==0);deletes.push_back(type);}
void DATATBLS_GetSequenceEvent(D2AnimSeqTxt*,int,int*){std::abort();}
void EVENT_SetEvent(D2GameStrc*,D2UnitStrc*,int type,int frame,int kind,int param){assert(param==0);events.push_back({type,frame,kind});}
'''+body+r'''
int main(){int count;assert(scanf("%d",&count)==1);printf("[");for(int i=0;i<count;i++){
 int speed,percent,elapsed,prefix;assert(scanf("%d%d%d%d",&speed,&percent,&elapsed,&prefix)==4);
 D2GameStrc game{200};Anim anim;anim.raw[15]=uint8_t(prefix);anim.pFrameFlags[4]=1;
 D2UnitStrc actor{(200-elapsed)*256,nullptr,0,0,speed,13*256,&anim};events.clear();deletes.clear();
 sub_6FCBCFD0(&game,&actor,percent);assert(deletes==std::vector<int>({1,2}));
 printf("%s{\"Speed\":%d,\"Percent\":%d,\"Elapsed\":%d,\"Prefix\":%d,\"Epoch256\":%d,\"Events\":[",i?",":"",speed,percent,elapsed,prefix,actor.nSeqCurrentFramePrecise);
 bool sep=false;for(auto e:events){printf("%s[%d,%d,%d]",sep?",":"",e[0],e[1],e[2]);sep=true;}printf("]}");}printf("]\n");}
'''
 cases=[(speed,pct,elapsed,0)for speed in [0,64,128,170,256,320,448]for pct in [0,50,100]for elapsed in [4,9]]
 cases += [(speed,100,4,4)for speed in [64,128,256,448]] # Declared counterfactual header controls,not current rows.
 path=STAGE/'source.cpp';path.write_text(cpp);binary=STAGE/'probe';request=str(len(cases))+'\n'+'\n'.join(' '.join(map(str,c))for c in cases)+'\n';(STAGE/'input.txt').write_text(request)
 run=subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-omit-frame-pointer','-fno-pie','-no-pie',str(path),'-o',str(binary)],capture_output=True,text=True)
 (STAGE/'compiler.log').write_text(run.stdout+run.stderr);assert run.returncode==0,run.stderr
 run=subprocess.run([str(binary)],input=request,capture_output=True,text=True);(STAGE/'sanitizer.log').write_text(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr
 data=json.loads(run.stdout);assert len(data)==46
 golden=next(c for c in data if(c['Speed'],c['Percent'],c['Elapsed'],c['Prefix'])==(256,100,4,0))
 assert golden['Epoch256']==200*256 and golden['Events']==[[1,204,1],[2,213,0]]
 fixture=STAGE/'cases.json';fixture.write_text(json.dumps(data,separators=(',',':'))+'\n');pin(str(Path(__file__).resolve().relative_to(ROOT)))
 receipt=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],contexts=46,currentHeaderContexts=42,counterfactualHeaderContexts=4,ownedRows=rows,
  completeRoutineHashes={'sub_6FCBCFD0':hashlib.sha256(body.encode()).hexdigest()},inputs=inputs,
  artifacts={str(p.relative_to(ROOT)):sha(p)for p in [path,binary,fixture,STAGE/'input.txt',STAGE/'compiler.log',STAGE/'sanitizer.log']},
  scope='Full ordinary-animation percent rewind with declared event deletion/insertion and no sequence. Real owned13-frame/base256/event4 kick rows have preceding frameFlags byte0,from high speed-header byte at0x0f. Source reads index-1 inside one supplied byte-array object representation;ASan/UBSan clean. At speed256,100percent rewind schedules next kick at+4 and end at+13.',
  limits=['Sequence-event branch aborts and is not qualified. Four nonzero-header controls are declared counterfactuals,not actual D2R animation records.','No runtime rewind port,NPC cached kick loop,modern event/input/charge timing or learned Shadow admission established.'])
 out=ROOT/'tools/reference/shadow-kick-rewind-source-evidence.json';out.write_text(json.dumps(receipt,indent=2)+'\n');print('Own NPC kick rewind:46 complete source controls,three actual headers,ASan/UBSan clean;native repeated action remains next.')
if __name__=='__main__':main()
