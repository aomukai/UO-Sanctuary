#!/usr/bin/env python3
"""Read only Sigil's verified LEA removal callback and tracking sort edge."""
import json
from pathlib import Path
from probe_warlock_runtime import ROOT,STAGE,Image,CodeReader,digest,disassemble
from pe_unwind_groups import UnwindGroups
def capture():
 stage=STAGE/'warlock-do-157-20261108';target=stage/'sigil-tracking-callback-20261108.json';assert not target.exists()
 parents=[stage/'capture.json',stage/'services-sigil-limit-services-20261108.json'];artifacts=[Path(__file__).resolve(),ROOT/'tools/probe_warlock_runtime.py',ROOT/'tools/pe_unwind_groups.py'];texts=[]
 for p,entry in zip(parents,[0x44a100,0x28fa10]):
  proof=json.loads(p.read_text());row=next(r for r in proof['records']if int(r['rva'],16)==entry);assert row['allUnwindFragmentsCaptured'];body=[];artifacts.append(p)
  for f in row['fragments']:
   binary=next(ROOT/n for n,h in proof['artifacts'].items()if n.endswith('.bin')and h==f['sha256']);asm=binary.with_suffix('.asm');assert digest(binary.read_bytes())==f['sha256']and digest(asm.read_bytes())==proof['artifacts'][str(asm.relative_to(ROOT))];body.append(asm.read_text());artifacts += [binary,asm]
  texts.append('\n'.join(body))
 assert 'lea    rax,[rip+'in texts[0]and'# 0x1403a7f30'in texts[0]and'call   0x140290000'in texts[0]
 assert 'call   0x140290910'in texts[1]
 image=Image();unwind=UnwindGroups(image);reader=CodeReader(image);records=[]
 try:
  reader.anchors()
  for rva in [0x3a7f30,0x290910]:
   row=dict(rva=hex(rva),fragments=[],semanticIdentityVerified=False)
   for i,(begin,end,u)in enumerate(unwind.fragments(rva)):
    size=end-begin;va=image.base+begin;entry=next((m for m in reader.maps if m[0]<=va and va+size<=m[1]),None);f=dict(rva=hex(begin),endRva=hex(end),unwindRva=hex(u),permissions=entry[2]if entry else'cross-map',captured=False)
    if entry and'r'in entry[2]and'x'in entry[2]and size<=4096 and reader.total+2*size<=8192:
     raw=reader.stable(begin,size,'code');p=stage/f'sigil-tracking-{rva:x}-part{i}.bin';p.write_bytes(raw);asm,bad=disassemble(p,begin,image.base);artifacts += [p,asm];f.update(captured=True,sha256=digest(raw),invalidInstructionMarkers=bad)
    else:f['reason']='Not readable executable or outside bounded capture;no forced read'
    row['fragments'].append(f)
   row['allUnwindFragmentsCaptured']=all(f['captured']for f in row['fragments']);records.append(row)
 finally:reader.close()
 result=dict(referenceVersion='3.3.93847',executableSha256=image.ref['executable']['sha256'],sourceReferencesVerified=True,referenceKinds=['do157 LEA argument passed to tracking wrapper','tracking core direct CALL sort'],process=reader.identity,processMemoryRead=True,bytesRead=reader.total,reads=reader.reads,records=records,processInput=False,ptraceAttach=False,securitySettingsChanged=False,gameMemoryWritten=False,artifacts={str(p.relative_to(ROOT)):digest(p.read_bytes())for p in sorted(set(artifacts))})
 target.write_text(json.dumps(result,indent=2)+'\n');print(json.dumps(dict(bytesRead=reader.total,records=[{k:r[k]for k in ['rva','allUnwindFragmentsCaptured']}for r in records])))
if __name__=='__main__':capture()
