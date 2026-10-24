#!/usr/bin/env python3
"""Execute pinned complete monster sequence initializer, advance and event selection.

The immutable current monseq table is retained. Table access, ordinary animation
and frame bonus are explicit services; no live actor or current binary is implied.
"""
from pathlib import Path
import argparse,hashlib,json,re,subprocess
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'
p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');args=p.parse_args()
tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());hashes={};routines={}
def extract(rel,name):
 raw=(STAGE/'d2moo-pinned-source'/rel).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel)
 hashes[rel]=hashlib.sha256(raw).hexdigest();s=raw.decode();m=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',s,re.M);assert m,name
 end=s.index('{',m.start())+1;depth=1
 while depth:depth+=(s[end]=='{')-(s[end]=='}');end+=1
 body=s[m.start():end];routines[name]=hashlib.sha256(body.encode()).hexdigest();return body
raw=(STAGE/'d2r-monseq.json').read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()=='352141d47a75e3562b3bfd816e4ebea318980ba5'
rows=[r for r in json.loads(raw).values() if r.get('sequence')=='seq_chargesentry'];assert len(rows)==15 and [r.get('event',0) for r in rows]==[0,0,1]+[0]*12
cpp=r'''
#include <cassert>
#include <cstdint>
#include <cstdio>
#include <initializer_list>
#define __stdcall
#define __fastcall
#define D2_ASSERT assert
using D2AnimSeqEvent=int;constexpr int ANIMSEQ_EVENT_NONE=0,UNITFLAG_SQGFXCHANGE=32;
struct D2AnimSeqTxt{unsigned nMode,nFrame;int nDir,nEvent;};
D2AnimSeqTxt rows[15];
struct D2UnitStrc{D2AnimSeqTxt* pAnimSeq=nullptr;unsigned dwSeqFrameCount=0,dwSeqFrame=0,dwSeqSpeed=0,dwSeqMode=0,dwFlags=0;int dwFrameCountPrecise=0,nSeqCurrentFramePrecise=0,nActionFrame=0,wAnimSpeed=0;};
D2AnimSeqTxt* DATATBLS_GetMonSeqTxtRecordFromUnit(D2UnitStrc*){return rows;}
int DATATBLS_GetSeqFramePointsCount(D2UnitStrc*){return 15*256;}
int DATATBLS_GetSeqFrameCount(D2UnitStrc*){return 15;}
void UNITS_SetAnimActionFrame(D2UnitStrc*,int){assert(false);}
int UNITS_GetFrameBonus(D2UnitStrc*){assert(false);return 0;}
'''
seq='source/D2Common/src/DataTbls/SequenceTbls.cpp';units='source/D2Common/src/Units/Units.cpp'
for rel,name in [(seq,'DATATBLS_ComputeSequenceAnimation'),(seq,'DATATBLS_GetSequenceEvent'),(units,'UNITS_InitializeSequence'),(units,'UNITS_StopSequence'),(units,'UNITS_IsAtEndOfFrameCycle')]:cpp+='\n'+extract(rel,name)+'\n'
cpp+=r'''
int main(){for(int i=0;i<15;i++)rows[i]={9u,unsigned(i),0,i==2?1:0};
 D2UnitStrc unit;UNITS_InitializeSequence(&unit);assert(unit.dwSeqFrameCount==3840&&unit.dwSeqFrame==0&&unit.dwSeqSpeed==256&&unit.dwFrameCountPrecise==3840&&unit.dwSeqMode==9&&unit.nActionFrame==0);
 printf("[");bool first=true;int total=0;
 for(int speed:{0,1,64,128,175,256,384,512,768,1024})for(int start:{0,255,256,511,512,767,768,3584,3839})for(int remaining:{1,256,1024,3840}){
  unit={};UNITS_InitializeSequence(&unit);unit.dwSeqFrame=start;unit.dwSeqSpeed=speed;unit.dwFrameCountPrecise=remaining;
  for(int step=0;step<20&&!UNITS_IsAtEndOfFrameCycle(&unit);step++){
   unsigned old=unit.dwSeqFrame;int before=unit.dwFrameCountPrecise;UNITS_StopSequence(&unit);assert(unit.dwSeqMode==9);
   printf("%s[%d,%u,%d,%u,%d,%d]",first?"":",",speed,old,before,unit.dwSeqFrame,unit.dwFrameCountPrecise,unit.nActionFrame);first=false;total++;
  }
 }
 printf("]\n");assert(total>2000);
}
'''
source=STAGE/'sentry-sequence-source-probe.cpp';source.write_text(cpp);binary=source.with_suffix('')
subprocess.run(['g++','-std=c++17','-Wall','-Wextra','-Werror',str(source),'-o',str(binary)],check=True)
result=json.loads(subprocess.check_output([str(binary)]));san=STAGE/'sentry-sequence-source-probe-san'
subprocess.run(['g++','-std=c++17','-g','-fsanitize=address,undefined','-fno-omit-frame-pointer',str(source),'-o',str(san)],check=True)
with (STAGE/'sentry-sequence-sanitizer.log').open('wb') as log:sanitized=subprocess.check_output([str(san)],stderr=log)
assert json.loads(sanitized)==result and not (STAGE/'sentry-sequence-sanitizer.log').read_bytes()
fixture=STAGE/'sentry-sequence-source-probe.json';fixture.write_text(json.dumps(result,separators=(',',':'))+'\n')
e={'referenceVersion':'3.3.93847','legacyCommit':tree['sha'],'sourceHashes':hashes,'routineHashes':routines,'table':{'url':'https://raw.githubusercontent.com/blizzhackers/d2data/fa1286949266498b294c40c2942521a7c0d7207a/json/monseq.json','commit':'fa1286949266498b294c40c2942521a7c0d7207a','blob':'352141d47a75e3562b3bfd816e4ebea318980ba5','sha256':hashlib.sha256(raw).hexdigest(),'rows':rows},'verification':{'initializationCases':1,'completeSequenceFrames':len(result)},'fixture':{'path':str(fixture.relative_to(ROOT)),'sha256':hashlib.sha256(fixture.read_bytes()).hexdigest()},'scope':'Complete unchanged initializer, sequence advance, event lookup and end test with pinned current sequence entries; ASan/UBSan clean.','limits':['Actual NPC mode-entry/dispatch/end callbacks, live animation rate and event scheduling are separate.','Table lookup, ordinary-animation branch and frame bonus are declared services; the non-sequence branch is not exercised.','Source zero speed and unusual starting fractions are explicit controls, not asserted gameplay states; current sentry animation-rate acceptance remains pending.'],'handlerEnabled':False}
path=ROOT/'tools/reference/sentry-sequence-source-evidence.json'
if args.check:assert json.loads(path.read_text())==e
else:path.write_text(json.dumps(e,indent=2)+'\n')
print(len(result),'complete unchanged sequence frame comparisons; sanitizers clean')
