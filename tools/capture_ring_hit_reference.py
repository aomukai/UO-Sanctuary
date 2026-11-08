#!/usr/bin/env python3
"""Bounded read-only Ring/Fire Ball hit1 candidate; table shape is not dispatcher proof."""
import argparse,json,os,re,struct
from pathlib import Path
from probe_warlock_runtime import ROOT,STAGE,Image,CodeReader,digest,disassemble,identity
from pe_unwind_groups import UnwindGroups
TABLE=0x163be50
ENTRY=0x3f0830

def capture(label):
 assert re.fullmatch('[a-z0-9-]+',label)
 target=STAGE/f'ring-hit1-{label}.json';assert not target.exists(),'Preserve existing observation'
 image=Image();raw=image.bytes(TABLE,72*8);pointers=struct.unpack('<72Q',raw)
 assert pointers[0]==0 and pointers[1]-image.base==ENTRY and pointers[23]-image.base==0x3f88c0 and pointers[41]-image.base==0x3fc7d0
 reader=CodeReader(image);unwind=UnwindGroups(image);fragments=[];artifacts=[Path(__file__).resolve(),ROOT/'tools/probe_warlock_runtime.py',ROOT/'tools/pe_unwind_groups.py']
 try:
  reader.anchors();va=image.base+TABLE;m=next(m for m in reader.maps if m[0]<=va and va+len(raw)<=m[1]);assert 'r'in m[2]and'w'not in m[2]and image.section(TABLE,len(raw))['name']=='.rdata'
  a=os.pread(reader.fd,len(raw),va);b=os.pread(reader.fd,len(raw),va);reader.total+=len(a)+len(b);assert a==b==raw and identity(reader.identity['pid'])==reader.identity
  reader.reads.append(dict(kind='immutable-candidate-hit-table',rva=hex(TABLE),size=len(raw),permissions=m[2],stable=True,sha256=digest(raw)))
  for i,(begin,end,u)in enumerate(unwind.fragments(ENTRY)):
   size=end-begin;va=image.base+begin;m=next((m for m in reader.maps if m[0]<=va and va+size<=m[1]),None);f=dict(rva=hex(begin),endRva=hex(end),unwindRva=hex(u),permissions=m[2]if m else'cross-map',captured=False)
   if m and'r'in m[2]and'x'in m[2]and size<=4096 and reader.total+size*2<=8192:
    data=reader.stable(begin,size,'code');p=STAGE/f'ring-hit1-{label}-part{i}.bin';p.write_bytes(data);asm,bad=disassemble(p,begin,image.base);artifacts.extend([p,asm]);f.update(captured=True,sha256=digest(data),invalidInstructionMarkers=bad)
   else:f['reason']='Protected/nonexecutable or outside bounded capture;no forced read'
   fragments.append(f)
 finally:reader.close()
 record=dict(rva=hex(ENTRY),bytes=sum(int(f['endRva'],16)-int(f['rva'],16)for f in fragments),allUnwindFragmentsCaptured=all(f['captured']for f in fragments),semanticIdentityVerified=False,fragments=fragments)
 result=dict(referenceVersion='3.3.93847',executableSha256=image.ref['executable']['sha256'],candidateTableRva=hex(TABLE),candidateSlot=1,immutableCandidateTableMatches=True,dispatcherIndexLinked=False,records=[record],process=reader.identity,processMemoryRead=True,bytesRead=reader.total,reads=reader.reads,processInput=False,ptraceAttach=False,securitySettingsChanged=False,gameMemoryWritten=False,artifacts={str(p.relative_to(ROOT)):digest(p.read_bytes())for p in artifacts})
 target.write_text(json.dumps(result,indent=2)+'\n');print(json.dumps(dict(bytesRead=reader.total,record=record)))
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--label',required=True);capture(p.parse_args().label)
