#!/usr/bin/env python3
"""Read bounded direct callees of the captured GenericPet candidate only."""
import argparse,json,re
from pathlib import Path
from probe_warlock_runtime import ROOT,STAGE,Image,CodeReader,digest,disassemble
from pe_unwind_groups import UnwindGroups

def capture(label,rvas):
 assert re.fullmatch('[a-z0-9-]+',label);target=STAGE/('generic-helpers-'+label+'.json');assert not target.exists(),'Preserve captures'
 parent=json.loads((STAGE/'generic-pet-candidate.json').read_text());assert parent['allUnwindFragmentsCaptured'];calls=set();parentartifacts={}
 for name,h in parent['artifacts'].items():
  p=ROOT/name;assert digest(p.read_bytes())==h
  if p.suffix=='.asm':calls.update(int(v,16)-0x140000000 for v in re.findall(r'call\s+0x([0-9a-f]+)',p.read_text()))
 assert set(rvas)<=calls,'Not a direct callee in the captured parent'
 image=Image();unwind=UnwindGroups(image);reader=CodeReader(image);records=[];artifacts=[]
 try:
  reader.anchors()
  for rva in rvas:
   parts=unwind.fragments(rva);record=dict(rva=hex(rva),bytes=sum(b-a for a,b,u in parts),fragments=[],semanticIdentityVerified=False)
   for i,(begin,end,u) in enumerate(parts):
    size=end-begin;va=image.base+begin;entry=next((m for m in reader.maps if m[0]<=va and va+size<=m[1]),None);item=dict(rva=hex(begin),endRva=hex(end),unwindRva=hex(u),permissions=entry[2] if entry else 'cross-map',captured=False)
    if entry and 'r' in entry[2] and 'x' in entry[2] and size<=4096 and reader.total+size*2<=8192:
     body=reader.stable(begin,size,'code');p=STAGE/f'generic-{label}-{rva:x}-part{i}.bin';p.write_bytes(body);asm,bad=disassemble(p,begin,image.base);artifacts += [p,asm];item.update(captured=True,sha256=digest(body),invalidInstructionMarkers=bad)
    else:item['reason']='Not readable executable or exceeds this bounded capture'
    record['fragments'].append(item)
   record['allUnwindFragmentsCaptured']=all(x['captured'] for x in record['fragments']);records.append(record)
 finally:reader.close()
 artifacts += [Path(__file__).resolve(),STAGE/'generic-pet-candidate.json',ROOT/'tools/probe_warlock_runtime.py',ROOT/'tools/pe_unwind_groups.py']
 result=dict(referenceVersion='3.3.93847',executableSha256=image.ref['executable']['sha256'],parentRva=parent['genericPetRva'],directCallTargetsVerified=True,process=reader.identity,processMemoryRead=True,bytesRead=reader.total,reads=reader.reads,records=records,processInput=False,ptraceAttach=False,securitySettingsChanged=False,gameMemoryWritten=False,artifacts={str(p.relative_to(ROOT)):digest(p.read_bytes()) for p in artifacts})
 target.write_text(json.dumps(result,indent=2)+'\n');print(json.dumps(dict(bytesRead=reader.total,records=[{k:r[k] for k in ['rva','bytes','allUnwindFragmentsCaptured']} for r in records])))
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--label',required=True);p.add_argument('--rvas',type=lambda x:int(x,0),nargs='+',required=True);a=p.parse_args();capture(a.label,a.rvas)
