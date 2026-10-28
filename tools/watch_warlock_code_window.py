#!/usr/bin/env python3
"""Wait briefly for one known-code window; never alter the running process."""
import json,os,struct,time
from pathlib import Path
from probe_warlock_runtime import ROOT,STAGE,Image,CodeReader,identity,digest,disassemble
from probe_warlock_event_reference import immutable_table
from pe_unwind_groups import UnwindGroups

def capture():
 target=STAGE/'event36-window.json';assert not target.exists(),'Preserve prior observation';image=Image();reader=CodeReader(image);start=time.monotonic();attempts=0;polls=0;captured=None
 try:
  reader.anchors();table=immutable_table(reader);assert table==image.bytes(0x163a750,328);rva=struct.unpack('<41Q',table)[36]-image.base;parts=UnwindGroups(image).fragments(rva);assert len(parts)==1 and parts[0][1]-rva==921
  expected=(STAGE/'inactive-known-code-sample.bin').read_bytes();assert len(expected)==32
  while time.monotonic()-start<45 and reader.total+1842<=65536:
   assert identity(reader.identity['pid'])==reader.identity
   lines=(Path('/proc')/str(reader.identity['pid'])/'maps').read_text().splitlines();polls+=1;entry=None
   for line in lines:
    fields=line.split();a,b=(int(x,16) for x in fields[0].split('-'))
    if a<=image.base+rva and image.base+rva+921<=b:entry=fields[1];break
   if entry and 'r' in entry and 'x' in entry:
    first=os.pread(reader.fd,921,image.base+rva);second=os.pread(reader.fd,921,image.base+rva);reader.total+=len(first)+len(second);attempts+=1
    if len(first)==921 and first==second and first[:32]==expected:
     captured=first;break
   time.sleep(0.05)
  assert identity(reader.identity['pid'])==reader.identity
 finally:reader.close()
 result=dict(referenceVersion='3.3.93847',executableSha256=image.ref['executable']['sha256'],process=reader.identity,rva=hex(rva),seconds=round(time.monotonic()-start,3),polls=polls,codeReadAttempts=attempts,bytesRead=reader.total,captured=captured is not None,processInput=False,ptraceAttach=False,securitySettingsChanged=False,gameMemoryWritten=False,callbackIdentityVerified=False,artifacts={})
 if captured:
  p=STAGE/'event36-window.bin';p.write_bytes(captured);asm,bad=disassemble(p,rva,image.base);result.update(invalidInstructionMarkers=bad,allUnwindFragmentsCaptured=True)
  for x in [p,asm]:result['artifacts'][str(x.relative_to(ROOT))]=digest(x.read_bytes())
 result['artifacts'][str(Path(__file__).resolve().relative_to(ROOT))]=digest(Path(__file__).resolve().read_bytes());target.write_text(json.dumps(result,indent=2)+'\n');print(json.dumps({k:v for k,v in result.items() if k not in ['artifacts','process']}))
if __name__=='__main__':capture()
