#!/usr/bin/env python3
"""Bounded read-only capture of the candidate missile-damage table and bodies.

Table shape and unwind membership are discovery evidence only. Dispatcher
linkage must be established separately before calling slot17 a Ring callback.
"""
import argparse,json,os,re,struct
from pathlib import Path
from probe_warlock_runtime import ROOT,STAGE,Image,CodeReader,digest,disassemble,identity
from pe_unwind_groups import UnwindGroups
TABLE=0x163c0b0
def capture(label,rvas):
 assert re.fullmatch('[a-z0-9-]+',label)
 # Candidate body and neighboring dispatcher-sized functions, selected from
 # the pinned unwind directory; no arbitrary memory address argument.
 assert len(rvas)==len(set(rvas)) and set(rvas)<={0x3fa2f0,0x3fabf0,0x3fb050,0x3fbf40}
 image=Image();unwind=UnwindGroups(image);raw=image.bytes(TABLE,31*8);pointers=struct.unpack('<31Q',raw)
 assert pointers[0]==0 and not any(pointers[18:])
 assert all(image.section(p-image.base)['name']=='.text' for p in pointers[1:18]) and pointers[17]-image.base==0x3fa2f0
 stage=STAGE/label;stage.mkdir(exist_ok=False);reader=CodeReader(image);records=[];artifacts=[Path(__file__).resolve(),ROOT/'tools/probe_warlock_runtime.py',ROOT/'tools/pe_unwind_groups.py']
 try:
  reader.anchors();va=image.base+TABLE;entry=next(m for m in reader.maps if m[0]<=va and va+len(raw)<=m[1]);assert 'r'in entry[2]and'w'not in entry[2] and image.section(TABLE,len(raw))['name']=='.rdata'
  first=os.pread(reader.fd,len(raw),va);second=os.pread(reader.fd,len(raw),va);reader.total+=len(first)+len(second)
  assert first==second==raw and identity(reader.identity['pid'])==reader.identity
  reader.reads.append(dict(kind='immutable-candidate-damage-table',rva=hex(TABLE),size=len(raw),permissions=entry[2],stable=True,sha256=digest(raw)))
  for rva in rvas:
   record=dict(rva=hex(rva),semanticIdentityVerified=False,fragments=[])
   for i,(begin,end,u)in enumerate(unwind.fragments(rva)):
    va=image.base+begin;size=end-begin;entry=next((m for m in reader.maps if m[0]<=va and va+size<=m[1]),None)
    f=dict(rva=hex(begin),endRva=hex(end),unwindRva=hex(u),permissions=entry[2]if entry else'cross-map',captured=False)
    if entry and'r'in entry[2]and'x'in entry[2]and size<=4096 and reader.total+size*2<=8192:
     data=reader.stable(begin,size,'code');p=stage/f'{rva:x}-part{i}.bin';p.write_bytes(data);asm,bad=disassemble(p,begin,image.base);artifacts += [p,asm];f.update(captured=True,sha256=digest(data),invalidInstructionMarkers=bad)
    else:f['reason']='Not readable executable or outside bounded capture; no forced read'
    record['fragments'].append(f)
   record['allUnwindFragmentsCaptured']=all(f['captured']for f in record['fragments']);records.append(record)
 finally:reader.close()
 result=dict(referenceVersion='3.3.93847',executableSha256=image.ref['executable']['sha256'],process=reader.identity,processMemoryRead=True,bytesRead=reader.total,reads=reader.reads,candidateTableRva=hex(TABLE),immutableCandidateTableMatches=True,tablePointers=[hex(p-image.base)if p else None for p in pointers],dispatcherIndexLinked=False,callbackSemanticsVerified=False,records=records,processInput=False,ptraceAttach=False,securitySettingsChanged=False,gameMemoryWritten=False,artifacts={str(p.relative_to(ROOT)):digest(p.read_bytes())for p in artifacts})
 (stage/'capture.json').write_text(json.dumps(result,indent=2)+'\n');print(json.dumps(dict(bytesRead=reader.total,records=[{k:r[k]for k in ['rva','allUnwindFragmentsCaptured']}for r in records])))
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--label',required=True);p.add_argument('--rvas',type=lambda v:int(v,0),nargs='+',required=True);a=p.parse_args();capture(a.label,a.rvas)
