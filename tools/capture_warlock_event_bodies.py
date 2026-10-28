#!/usr/bin/env python3
"""Capture specified event candidates with all known x64 unwind fragments."""
import argparse,json,struct
from pathlib import Path
from probe_warlock_runtime import ROOT,STAGE,Image,CodeReader,digest,disassemble
from probe_warlock_event_reference import immutable_table
from pe_unwind_groups import UnwindGroups

def capture(slots):
 label='event-bodies-'+'-'.join(str(x) for x in slots);target=STAGE/(label+'.json');assert not target.exists(),'Preserve prior captures';assert set(slots)<={17,28,35,36,37,39,40}
 image=Image();unwind=UnwindGroups(image);reader=CodeReader(image);artifacts=[];records=[]
 try:
  reader.anchors();raw=immutable_table(reader);assert raw==image.bytes(0x163a750,41*8);pointers=struct.unpack('<41Q',raw)
  for slot in slots:
   rva=pointers[slot]-image.base;fragments=unwind.fragments(rva);item=dict(slot=slot,rva=hex(rva),totalBytes=sum(b-a for a,b,u in fragments),unwindFragmentCount=len(fragments),identityVerified=False,fragments=[])
   for i,(begin,end,u) in enumerate(fragments):
    va=image.base+begin;size=end-begin;entry=next((m for m in reader.maps if m[0]<=va and va+size<=m[1]),None)
    record=dict(rva=hex(begin),endRva=hex(end),unwindRva=hex(u),permissions=entry[2] if entry else 'cross-map',captured=False)
    if entry and 'r' in entry[2] and 'x' in entry[2] and reader.total+size*2<=8192 and size<=4096:
     body=reader.stable(begin,size,'code');p=STAGE/f'{label}-slot{slot}-part{i}.bin';p.write_bytes(body);asm,bad=disassemble(p,begin,image.base);artifacts += [p,asm];record.update(captured=True,sha256=digest(body),invalidInstructionMarkers=bad)
    else:record['reason']='Not a readable executable range or outside this capture budget'
    item['fragments'].append(record)
   item['allUnwindFragmentsCaptured']=all(f['captured'] for f in item['fragments']);records.append(item)
 finally:reader.close()
 artifacts += [Path(__file__).resolve(),ROOT/'tools/probe_warlock_runtime.py',ROOT/'tools/probe_warlock_event_reference.py',ROOT/'tools/pe_unwind_groups.py']
 result=dict(referenceVersion='3.3.93847',executableSha256=image.ref['executable']['sha256'],process=reader.identity,processMemoryRead=True,bytesRead=reader.total,reads=reader.reads,immutableCandidateTableMatches=True,records=records,processInput=False,ptraceAttach=False,securitySettingsChanged=False,gameMemoryWritten=False,callbackIdentitiesVerified=False,artifacts={str(p.relative_to(ROOT)):digest(p.read_bytes()) for p in artifacts})
 target.write_text(json.dumps(result,indent=2)+'\n');print(json.dumps(dict(bytesRead=reader.total,records=[{k:r[k] for k in ['slot','totalBytes','unwindFragmentCount','allUnwindFragmentsCaptured']} for r in records])))
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--slots',type=int,nargs='+',required=True);a=p.parse_args();capture(a.slots)
