#!/usr/bin/env python3
"""Bounded readonly capture of the callback directly LEA-linked by retained missile evaluator."""
import argparse,json,re
from pathlib import Path
from probe_warlock_runtime import ROOT,STAGE,Image,CodeReader,digest,disassemble
from pe_unwind_groups import UnwindGroups

def capture(label):
 assert re.fullmatch('[a-z0-9-]+',label)
 parent=STAGE/'ring-damage-body-20261108/services-ring-damage-formula-20261108.json';p=json.loads(parent.read_text());record=next(r for r in p['records']if r['rva']=='0x2bb3a0');assert record['allUnwindFragmentsCaptured']
 artifacts=[parent,Path(__file__).resolve(),ROOT/'tools/probe_warlock_runtime.py',ROOT/'tools/pe_unwind_groups.py'];texts=[]
 for f in record['fragments']:
  binary=next(ROOT/n for n,h in p['artifacts'].items()if n.endswith('.bin')and h==f['sha256']);asm=binary.with_suffix('.asm')
  assert digest(binary.read_bytes())==f['sha256']and digest(asm.read_bytes())==p['artifacts'][str(asm.relative_to(ROOT))];artifacts.extend([binary,asm]);texts.append(asm.read_text())
 text='\n'.join(texts);assert 'lea    r9,'in text and '# 0x1402bb350'in text and 'call   0x1407c9920'in text
 target=STAGE/f'ring-formula-binding-{label}.json';assert not target.exists()
 image=Image();reader=CodeReader(image);unwind=UnwindGroups(image);records=[]
 try:
  reader.anchors();fragments=[]
  for index,(begin,end,u)in enumerate(unwind.fragments(0x2bb350)):
   size=end-begin;va=image.base+begin;m=next((m for m in reader.maps if m[0]<=va and va+size<=m[1]),None);f=dict(rva=hex(begin),endRva=hex(end),unwindRva=hex(u),permissions=m[2]if m else'cross-map',captured=False)
   if m and'r'in m[2]and'x'in m[2]and size<=4096 and reader.total+size*2<=8192:
    raw=reader.stable(begin,size,'code');binary=STAGE/f'ring-formula-binding-{label}-part{index}.bin';binary.write_bytes(raw);asm,bad=disassemble(binary,begin,image.base);artifacts.extend([binary,asm]);f.update(captured=True,sha256=digest(raw),invalidInstructionMarkers=bad)
   else:f['reason']='Protected/nonexecutable or outside bounded budget;no forced read'
   fragments.append(f)
 finally:reader.close()
 result=dict(referenceVersion='3.3.93847',executableSha256=image.ref['executable']['sha256'],sourceReferencesVerified=True,parentFunction='0x2bb3a0',callbackRva='0x2bb350',allUnwindFragmentsCaptured=all(f['captured']for f in fragments),fragments=fragments,process=reader.identity,processMemoryRead=True,bytesRead=reader.total,reads=reader.reads,processInput=False,ptraceAttach=False,securitySettingsChanged=False,gameMemoryWritten=False,artifacts={str(a.relative_to(ROOT)):digest(a.read_bytes())for a in artifacts})
 target.write_text(json.dumps(result,indent=2)+'\n');print(json.dumps({k:result[k]for k in ['callbackRva','allUnwindFragmentsCaptured','bytesRead']}))
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--label',required=True);capture(p.parse_args().label)
