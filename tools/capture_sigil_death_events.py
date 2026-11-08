#!/usr/bin/env python3
"""Bounded read-only Sigil Death event33/34 candidates;no dispatcher identity inferred."""
import argparse,json,re,struct
from pathlib import Path
from probe_warlock_runtime import ROOT,STAGE,Image,CodeReader,digest,disassemble
from probe_warlock_event_reference import immutable_table
from pe_unwind_groups import UnwindGroups

def capture(label):
 assert re.fullmatch('[a-z0-9-]+',label)
 target=STAGE/f'sigil-death-events-{label}.json';assert not target.exists()
 image=Image();unwind=UnwindGroups(image);reader=CodeReader(image);records=[];artifacts=[Path(__file__).resolve(),ROOT/'tools/probe_warlock_runtime.py',ROOT/'tools/probe_warlock_event_reference.py',ROOT/'tools/pe_unwind_groups.py']
 try:
  reader.anchors();raw=immutable_table(reader);assert raw==image.bytes(0x163a750,41*8);pointers=struct.unpack('<41Q',raw)
  for slot in [33,34]:
   rva=pointers[slot]-image.base;parts=unwind.fragments(rva);fragments=[]
   for i,(begin,end,u)in enumerate(parts):
    size=end-begin;va=image.base+begin;m=next((m for m in reader.maps if m[0]<=va and va+size<=m[1]),None);f=dict(rva=hex(begin),endRva=hex(end),unwindRva=hex(u),permissions=m[2]if m else'cross-map',captured=False)
    if m and'r'in m[2]and'x'in m[2]and size<=4096 and reader.total+size*2<=8192:
     data=reader.stable(begin,size,'code');p=STAGE/f'sigil-death-events-{label}-{slot}-part{i}.bin';p.write_bytes(data);asm,bad=disassemble(p,begin,image.base);artifacts.extend([p,asm]);f.update(captured=True,sha256=digest(data),invalidInstructionMarkers=bad)
    else:f['reason']='Protected/nonexecutable or outside bounded budget;no forced read'
    fragments.append(f)
   records.append(dict(slot=slot,rva=hex(rva),bytes=sum(b-a for a,b,u in parts),allUnwindFragmentsCaptured=all(f['captured']for f in fragments),fragments=fragments))
 finally:reader.close()
 result=dict(referenceVersion='3.3.93847',executableSha256=image.ref['executable']['sha256'],immutableCandidateTableMatches=True,dispatcherIndexLinked=False,semanticIdentityVerified=False,records=records,process=reader.identity,processMemoryRead=True,bytesRead=reader.total,reads=reader.reads,processInput=False,ptraceAttach=False,securitySettingsChanged=False,gameMemoryWritten=False,artifacts={str(p.relative_to(ROOT)):digest(p.read_bytes())for p in artifacts})
 target.write_text(json.dumps(result,indent=2)+'\n');print(json.dumps(dict(bytesRead=reader.total,records=[{k:r[k]for k in ['slot','rva','bytes','allUnwindFragmentsCaptured']}for r in records])))
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--label',required=True);capture(p.parse_args().label)
