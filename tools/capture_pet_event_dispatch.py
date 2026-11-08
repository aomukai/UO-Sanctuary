#!/usr/bin/env python3
"""Inspect one bounded unwind candidate beside the qualified event scheduler.

Proximity selects a candidate only. Semantics require the captured body/calls.
"""
import json
from probe_warlock_runtime import ROOT,STAGE,Image,CodeReader,digest,disassemble
from pe_unwind_groups import UnwindGroups

def capture():
    parent=STAGE/'callees-pet-ai-event-20261109.json';receipt=json.loads(parent.read_text())
    assert receipt['directCallTargetsVerified'] and receipt['records'][0]['rva']=='0x32f9a0' and receipt['records'][0]['allUnwindFragmentsCaptured']
    for n,h in receipt['artifacts'].items():assert digest((ROOT/n).read_bytes())==h,n
    output=STAGE/'pet-event-dispatch-candidate-20261109.json';assert not output.exists(),'Preserve earlier reads'
    image=Image();groups=UnwindGroups(image);rva=0x32f900;parts=groups.fragments(rva)
    assert sum(b-a for a,b,u in parts)==145 and max(b for a,b,u in parts)<=0x32f9a0
    reader=CodeReader(image);record=dict(rva=hex(rva),bytes=145,fragments=[],semanticIdentityVerified=False);artifacts=[]
    try:
        reader.anchors()
        for i,(a,b,u) in enumerate(parts):
            va=image.base+a;entry=next((m for m in reader.maps if m[0]<=va and va+b-a<=m[1]),None)
            item=dict(rva=hex(a),endRva=hex(b),unwindRva=hex(u),permissions=entry[2]if entry else 'cross-map',captured=False)
            if entry and 'r'in entry[2] and 'x'in entry[2]:
                raw=reader.stable(a,b-a,'code');path=STAGE/f'pet-event-dispatch-candidate-20261109-{rva:x}-part{i}.bin';path.write_bytes(raw);asm,bad=disassemble(path,a,image.base);artifacts.extend([path,asm]);item.update(captured=True,sha256=digest(raw),invalidInstructionMarkers=bad)
            else:item['reason']='Not readable executable;no forced read'
            record['fragments'].append(item)
        record['allUnwindFragmentsCaptured']=all(f['captured']for f in record['fragments'])
    finally:reader.close()
    artifacts.extend([parent,image.reference,ROOT/'tools/probe_warlock_runtime.py',ROOT/'tools/pe_unwind_groups.py',ROOT/'tools/capture_pet_event_dispatch.py'])
    result=dict(referenceVersion='3.3.93847',executableSha256=image.ref['executable']['sha256'],selection='Primary unwind candidate immediately before qualified32f9a0;proximity is not semantic evidence',records=[record],process=reader.identity,processMemoryRead=True,bytesRead=reader.total,reads=reader.reads,processInput=False,ptraceAttach=False,securitySettingsChanged=False,gameMemoryWritten=False,artifacts={str(p.relative_to(ROOT)):digest(p.read_bytes())for p in artifacts})
    output.write_text(json.dumps(result,indent=2)+'\n');print(json.dumps(dict(bytesRead=reader.total,records=[{k:record[k]for k in ['rva','bytes','allUnwindFragmentsCaptured']}])) )
if __name__=='__main__':capture()
