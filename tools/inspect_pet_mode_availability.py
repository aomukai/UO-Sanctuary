#!/usr/bin/env python3
"""Record permissions of known mode callees without reading process memory."""
import json,re
from pathlib import Path
from probe_warlock_runtime import ROOT,STAGE,Image,processes,identity,digest
from pe_unwind_groups import UnwindGroups
def inspect():
    parent=STAGE/'callees-live-movement-core-20261029.json';p=json.loads(parent.read_text());calls=set()
    for name,h in p['artifacts'].items():
        path=ROOT/name;assert digest(path.read_bytes())==h,name
        if path.suffix=='.asm':calls.update(int(v,16)-0x140000000 for v in re.findall(r'call\s+0x([0-9a-f]+)',path.read_text()))
    targets=[0x3de170,0x3dda90];assert set(targets)<=calls
    image=Image();process=processes();assert len(process)==1;process=process[0];maps=[]
    for line in (Path('/proc')/str(process['pid'])/'maps').read_text().splitlines():
        parts=line.split();a,b=(int(v,16) for v in parts[0].split('-'));maps.append((a,b,parts[1]))
    records=[]
    for target in targets:
        fragments=[]
        for a,b,u in UnwindGroups(image).fragments(target):
            permissions=next((v for x,y,v in maps if x<=image.base+a and image.base+b<=y),'cross-map')
            fragments.append(dict(rva=hex(a),endRva=hex(b),unwindRva=hex(u),permissions=permissions,readableExecutable='r' in permissions and 'x' in permissions))
        records.append(dict(rva=hex(target),bytes=sum(int(f['endRva'],16)-int(f['rva'],16) for f in fragments),fragments=fragments))
    assert identity(process['pid'])==process
    paths=[Path(__file__).resolve(),parent,image.reference,ROOT/'tools/probe_warlock_runtime.py',ROOT/'tools/pe_unwind_groups.py']
    result=dict(referenceVersion='3.3.93847',executableSha256=image.ref['executable']['sha256'],process=process,records=records,
        directCalleeReferencesVerified=True,processMemoryBytesRead=0,processInput=False,ptraceAttach=False,securitySettingsChanged=False,
        gameMemoryWritten=False,mechanicsVerified=False,artifacts={str(q.relative_to(ROOT)):digest(q.read_bytes()) for q in paths})
    out=ROOT/'.local/class-skills/grim-ward-curse-source/pet-mode-availability.json';assert not out.exists(),'Preserve prior inspection'
    out.write_text(json.dumps(result,indent=2)+'\n')
    print(json.dumps(dict(functions=len(records),fragments=sum(len(r['fragments']) for r in records),readableExecutableFragments=sum(f['readableExecutable'] for r in records for f in r['fragments']),processMemoryBytesRead=0)))
if __name__=='__main__':inspect()
