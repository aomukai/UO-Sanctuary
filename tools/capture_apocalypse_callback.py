#!/usr/bin/env python3
"""Read only the callback linked by the already-captured current Apocalypse do163."""
import argparse,json,re
from pathlib import Path
from probe_warlock_runtime import ROOT,STAGE,Image,CodeReader,digest,disassemble
from pe_unwind_groups import UnwindGroups
def capture(label):
 assert re.fullmatch('[a-z0-9-]+',label)
 parent=STAGE/'warlock-do-163-20261108/capture.json';p=json.loads(parent.read_text());r=next(r for r in p['records']if r['slot']==163)
 assert p['dispatcherIndexLinked']and r['rva']=='0x450410'and r['allUnwindFragmentsCaptured']
 artifacts=[parent,Path(__file__).resolve(),ROOT/'tools/probe_warlock_runtime.py',ROOT/'tools/pe_unwind_groups.py'];texts=[]
 for f in r['fragments']:
  binary=next(ROOT/n for n,h in p['artifacts'].items()if n.endswith('.bin')and h==f['sha256']);asm=binary.with_suffix('.asm')
  assert digest(binary.read_bytes())==f['sha256']and digest(asm.read_bytes())==p['artifacts'][str(asm.relative_to(ROOT))]
  artifacts.extend([binary,asm]);texts.append(asm.read_text())
 text='\n'.join(texts);assert text.count('# 0x140450090')==1 and 'call   0x1403a6160'in text
 target=STAGE/f'apocalypse-callback-{label}.json';assert not target.exists(),'Preserve earlier captures'
 image=Image();reader=CodeReader(image);unwind=UnwindGroups(image);fragments=[]
 try:
  reader.anchors()
  for index,(begin,end,u)in enumerate(unwind.fragments(0x450090)):
   size=end-begin;va=image.base+begin;m=next((m for m in reader.maps if m[0]<=va and va+size<=m[1]),None)
   f=dict(rva=hex(begin),endRva=hex(end),unwindRva=hex(u),permissions=m[2]if m else'cross-map',captured=False)
   if m and'r'in m[2]and'x'in m[2]and size<=4096 and reader.total+size*2<=8192:
    raw=reader.stable(begin,size,'code');binary=STAGE/f'apocalypse-callback-{label}-part{index}.bin';binary.write_bytes(raw)
    asm,bad=disassemble(binary,begin,image.base);artifacts.extend([binary,asm]);f.update(captured=True,sha256=digest(raw),invalidInstructionMarkers=bad)
   else:f['reason']='Protected/nonexecutable or outside bounded budget;no forced read'
   fragments.append(f)
 finally:reader.close()
 result=dict(referenceVersion='3.3.93847',executableSha256=image.ref['executable']['sha256'],sourceReferencesVerified=True,parentFunction='0x450410',callbackRva='0x450090',callbackSemanticsVerified=False,
  process=reader.identity,processMemoryRead=True,bytesRead=reader.total,reads=reader.reads,allUnwindFragmentsCaptured=all(f['captured']for f in fragments),fragments=fragments,
  processInput=False,ptraceAttach=False,securitySettingsChanged=False,gameMemoryWritten=False,artifacts={str(a.relative_to(ROOT)):digest(a.read_bytes())for a in artifacts})
 target.write_text(json.dumps(result,indent=2)+'\n');print(json.dumps({k:result[k]for k in ['callbackRva','allUnwindFragmentsCaptured','bytesRead','process']}))
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--label',required=True);capture(p.parse_args().label)
