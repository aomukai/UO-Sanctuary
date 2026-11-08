#!/usr/bin/env python3
"""Bounded read-only callees/recipient callback linked by retained current Cleave do164."""
import argparse,json,re
from pathlib import Path
from probe_warlock_runtime import ROOT,STAGE,Image,CodeReader,digest,disassemble
from pe_unwind_groups import UnwindGroups

def capture(label):
 assert re.fullmatch('[a-z0-9-]+',label)
 parent=STAGE/'warlock-do-164-20261108/capture.json';proof=json.loads(parent.read_text());r=proof['records'][0]
 assert r['slot']==164 and r['allUnwindFragmentsCaptured'] and proof['dispatcherIndexLinked']
 texts=[];artifacts=[parent,Path(__file__).resolve(),ROOT/'tools/probe_warlock_runtime.py',ROOT/'tools/pe_unwind_groups.py']
 for n,h in proof['artifacts'].items():
  p=ROOT/n;assert digest(p.read_bytes())==h,n
  if p.suffix=='.asm':texts.append(p.read_text());artifacts.append(p)
 text='\n'.join(texts);assert '# 0x140450780'in text
 for rva in [0x285590,0x27c360]:assert f'call   {hex(0x140000000+rva)}'in text
 target=STAGE/f'cleave-services-{label}.json';assert not target.exists()
 image=Image();unwind=UnwindGroups(image);reader=CodeReader(image);records=[]
 try:
  reader.anchors()
  for rva in [0x450780,0x285590,0x27c360]:
   parts=unwind.fragments(rva);fragments=[]
   for i,(begin,end,u)in enumerate(parts):
    size=end-begin;va=image.base+begin;m=next((m for m in reader.maps if m[0]<=va and va+size<=m[1]),None);f=dict(rva=hex(begin),endRva=hex(end),unwindRva=hex(u),permissions=m[2]if m else'cross-map',captured=False)
    if m and'r'in m[2]and'x'in m[2]and size<=4096 and reader.total+size*2<=8192:
     data=reader.stable(begin,size,'code');p=STAGE/f'cleave-services-{label}-{rva:x}-part{i}.bin';p.write_bytes(data);asm,bad=disassemble(p,begin,image.base);artifacts.extend([p,asm]);f.update(captured=True,sha256=digest(data),invalidInstructionMarkers=bad)
    else:f['reason']='Protected/nonexecutable or outside bounded capture;no forced read'
    fragments.append(f)
   records.append(dict(rva=hex(rva),bytes=sum(b-a for a,b,u in parts),allUnwindFragmentsCaptured=all(f['captured']for f in fragments),fragments=fragments))
 finally:reader.close()
 result=dict(referenceVersion='3.3.93847',executableSha256=image.ref['executable']['sha256'],sourceReferencesVerified=True,parent=str(parent.relative_to(ROOT)),records=records,process=reader.identity,processMemoryRead=True,bytesRead=reader.total,reads=reader.reads,processInput=False,ptraceAttach=False,securitySettingsChanged=False,gameMemoryWritten=False,artifacts={str(p.relative_to(ROOT)):digest(p.read_bytes())for p in artifacts})
 target.write_text(json.dumps(result,indent=2)+'\n');print(json.dumps(dict(bytesRead=reader.total,records=[{k:r[k]for k in ['rva','bytes','allUnwindFragmentsCaptured']}for r in records])))
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--label',required=True);capture(p.parse_args().label)
