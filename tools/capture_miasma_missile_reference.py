#!/usr/bin/env python3
"""Bounded read-only candidate missile-do41/45 capture; dispatcher linkage remains separate."""
import argparse,json,os,re,struct
from pathlib import Path
from probe_warlock_runtime import ROOT,STAGE,Image,CodeReader,digest,disassemble,identity
from pe_unwind_groups import UnwindGroups
TABLE=0x163bc90
CANDIDATES={41:0x3fb680,45:0x3fdbb0}
def capture(label,slot):
 assert re.fullmatch('[a-z0-9-]+',label)and slot in CANDIDATES
 target=STAGE/f'miasma-missile-{slot}-{label}.json';assert not target.exists(),'Preserve prior observation'
 image=Image();raw=image.bytes(TABLE,53*8);pointers=struct.unpack('<53Q',raw)
 assert pointers[0]==0 and pointers[1]-image.base==0x3fb040 and pointers[23]==pointers[24] and pointers[23]-image.base==0x3edfd0 and pointers[slot]-image.base==CANDIDATES[slot]
 reader=CodeReader(image);unwind=UnwindGroups(image);fragments=[];artifacts=[Path(__file__).resolve(),ROOT/'tools/probe_warlock_runtime.py',ROOT/'tools/pe_unwind_groups.py']
 try:
  reader.anchors();va=image.base+TABLE;m=next(m for m in reader.maps if m[0]<=va and va+len(raw)<=m[1]);assert 'r'in m[2]and'w'not in m[2]and image.section(TABLE,len(raw))['name']=='.rdata'
  a=os.pread(reader.fd,len(raw),va);b=os.pread(reader.fd,len(raw),va);reader.total+=len(a)+len(b);assert a==b==raw and identity(reader.identity['pid'])==reader.identity
  reader.reads.append(dict(kind='immutable-candidate-missile-do-table',rva=hex(TABLE),size=len(raw),permissions=m[2],stable=True,sha256=digest(raw)))
  for i,(begin,end,u)in enumerate(unwind.fragments(CANDIDATES[slot])):
   size=end-begin;va=image.base+begin;m=next((m for m in reader.maps if m[0]<=va and va+size<=m[1]),None);f=dict(rva=hex(begin),endRva=hex(end),unwindRva=hex(u),permissions=m[2]if m else'cross-map',captured=False)
   if m and'r'in m[2]and'x'in m[2]and size<=4096 and reader.total+size*2<=8192:
    data=reader.stable(begin,size,'code');p=STAGE/f'miasma-missile-{slot}-{label}-part{i}.bin';p.write_bytes(data);asm,bad=disassemble(p,begin,image.base);artifacts.extend([p,asm]);f.update(captured=True,sha256=digest(data),invalidInstructionMarkers=bad)
   else:f['reason']='Protected/nonexecutable or outside bounded capture;no forced read'
   fragments.append(f)
 finally:reader.close()
 result=dict(referenceVersion='3.3.93847',executableSha256=image.ref['executable']['sha256'],candidateTableRva=hex(TABLE),candidateSlot=slot,candidateRva=hex(CANDIDATES[slot]),immutableCandidateTableMatches=True,dispatcherIndexLinked=False,semanticIdentityVerified=False,allUnwindFragmentsCaptured=all(f['captured']for f in fragments),bytes=sum(int(f['endRva'],16)-int(f['rva'],16)for f in fragments),fragments=fragments,process=reader.identity,processMemoryRead=True,bytesRead=reader.total,reads=reader.reads,processInput=False,ptraceAttach=False,securitySettingsChanged=False,gameMemoryWritten=False,artifacts={str(p.relative_to(ROOT)):digest(p.read_bytes())for p in artifacts})
 target.write_text(json.dumps(result,indent=2)+'\n');print(json.dumps({k:result[k]for k in ['candidateSlot','candidateRva','bytes','allUnwindFragmentsCaptured','bytesRead']}))
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--label',required=True);p.add_argument('--slot',type=int,choices=CANDIDATES,required=True);a=p.parse_args();capture(a.label,a.slot)
