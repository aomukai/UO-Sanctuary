#!/usr/bin/env python3
"""Bounded current mastery selector linked by the retained maximum getter."""
import json
from pathlib import Path
from probe_warlock_runtime import ROOT,STAGE,Image,CodeReader,digest,disassemble
from pe_unwind_groups import UnwindGroups

def capture():
    parent=STAGE/'callees-apocalypse-packet-leaves-20261109.json';proof=json.loads(parent.read_text());entry=next(r for r in proof['records']if r['rva']=='0x28d050');assert entry['allUnwindFragmentsCaptured']
    artifacts=[parent,Path(__file__).resolve(),ROOT/'tools/probe_warlock_runtime.py',ROOT/'tools/pe_unwind_groups.py'];texts=[]
    for f in entry['fragments']:
        binary=next(ROOT/n for n,h in proof['artifacts'].items()if n.endswith('.bin')and h==f['sha256']);asm=binary.with_suffix('.asm')
        assert digest(binary.read_bytes())==f['sha256']and digest(asm.read_bytes())==proof['artifacts'][str(asm.relative_to(ROOT))]
        artifacts.extend([binary,asm]);texts.append(asm.read_text())
    assert 'call   0x14028cc10'in '\n'.join(texts)
    target=STAGE/'bane-mastery-selector-20261109.json';assert not target.exists(),'Preserve captures;do not repeat unchanged observations'
    image=Image();unwind=UnwindGroups(image);reader=CodeReader(image);fragments=[]
    try:
        reader.anchors()
        for i,(begin,end,u)in enumerate(unwind.fragments(0x28cc10)):
            va=image.base+begin;size=end-begin;m=next((m for m in reader.maps if m[0]<=va and va+size<=m[1]),None)
            f=dict(rva=hex(begin),endRva=hex(end),unwindRva=hex(u),permissions=m[2]if m else'cross-map',captured=False)
            if m and'r'in m[2]and'x'in m[2]and size<=4096 and reader.total+size*2<=8192:
                raw=reader.stable(begin,size,'code');p=STAGE/f'bane-mastery-selector-20261109-part{i}.bin';p.write_bytes(raw);asm,bad=disassemble(p,begin,image.base);artifacts.extend([p,asm]);f.update(captured=True,sha256=digest(raw),invalidInstructionMarkers=bad)
            else:f['reason']='Protected/nonexecutable or outside bounded budget;no forced read'
            fragments.append(f)
    finally:reader.close()
    result=dict(referenceVersion='3.3.93847',executableSha256=image.ref['executable']['sha256'],parent=str(parent.relative_to(ROOT)),parentRva='0x28d050',calleeRva='0x28cc10',directCallVerified=True,semanticIdentityVerified=False,process=reader.identity,processMemoryRead=True,bytesRead=reader.total,reads=reader.reads,allUnwindFragmentsCaptured=all(f['captured']for f in fragments),fragments=fragments,processInput=False,ptraceAttach=False,securitySettingsChanged=False,gameMemoryWritten=False,artifacts={str(p.relative_to(ROOT)):digest(p.read_bytes())for p in artifacts})
    target.write_text(json.dumps(result,indent=2)+'\n');print(json.dumps({k:result[k]for k in ['calleeRva','allUnwindFragmentsCaptured','bytesRead','fragments']}))
if __name__=='__main__':capture()
