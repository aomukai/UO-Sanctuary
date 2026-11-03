#!/usr/bin/env python3
"""Read only the owner callback referenced by the pinned pet initializer."""
import json,struct
from pathlib import Path
from probe_warlock_runtime import ROOT,Image,CodeReader,digest,disassemble
from pe_unwind_groups import UnwindGroups

STAGE=ROOT/'.local/class-skills/pet-owner-reference-20261103'
PARENT=ROOT/'.local/class-skills/warlock-runtime-reference/generic-helpers-actions.json'

def capture():
    parent=json.loads(PARENT.read_text())
    assert parent['referenceVersion']=='3.3.93847'
    for name,h in parent['artifacts'].items():assert digest((ROOT/name).read_bytes())==h,name
    record=next(r for r in parent['records'] if r['rva']=='0x4b0b90')
    assert record['allUnwindFragmentsCaptured']
    fragment=next(f for f in record['fragments'] if int(f['rva'],16)<=0x4b0e82<int(f['endRva'],16))
    paths=[ROOT/n for n,h in parent['artifacts'].items() if n.endswith('.bin') and h==fragment['sha256']]
    assert len(paths)==1
    source=paths[0];data=source.read_bytes();start=0x4b0e82-int(fragment['rva'],16)
    sequence=data[start:start+33]
    assert sequence==bytes.fromhex('4c8d4c24304c897424304c8d051dfaffff48897c2438498bd7498bcce82dd8eaff')
    callback=0x4b0e8c+7+struct.unpack('<i',sequence[13:17])[0]
    iterator=0x4b0e9e+5+struct.unpack('<i',sequence[-4:])[0]
    assert callback==0x4b08b0 and iterator==0x35e6d0
    STAGE.mkdir(exist_ok=True);out=STAGE/'owner-callback.json'
    assert not out.exists(),'Preserve prior capture'
    image=Image();unwind=UnwindGroups(image);reader=CodeReader(image)
    artifacts=[PARENT,source,Path(__file__).resolve(),ROOT/'tools/probe_warlock_runtime.py',ROOT/'tools/pe_unwind_groups.py'];parts=[]
    try:
        reader.anchors()
        for index,(begin,end,u) in enumerate(unwind.fragments(callback)):
            size=end-begin
            entry=next((m for m in reader.maps if m[0]<=image.base+begin and image.base+end<=m[1]),None)
            item=dict(rva=hex(begin),endRva=hex(end),unwindRva=hex(u),permissions=entry[2] if entry else 'cross-map',captured=False)
            if entry and 'r' in entry[2] and 'x' in entry[2] and size<=4096 and reader.total+size*2<=8192:
                b=reader.stable(begin,size,'code');p=STAGE/f'owner-callback-part{index}.bin';p.write_bytes(b)
                asm,bad=disassemble(p,begin,image.base);artifacts += [p,asm]
                item.update(captured=True,sha256=digest(b),invalidInstructionMarkers=bad)
            else:item['reason']='Not readable executable or outside bounded capture; no code read attempted'
            parts.append(item)
    finally:reader.close()
    result=dict(referenceVersion='3.3.93847',executableSha256=image.ref['executable']['sha256'],parent=str(PARENT.relative_to(ROOT)),
        callbackReference=dict(siteRva='0x4b0e82',callbackRva=hex(callback),iteratorRva=hex(iterator),argumentRegister='R8',sequenceSha256=digest(sequence)),
        process=reader.identity,processMemoryRead=True,bytesRead=reader.total,reads=reader.reads,
        records=[dict(rva=hex(callback),bytes=sum(int(f['endRva'],16)-int(f['rva'],16) for f in parts),fragments=parts,allUnwindFragmentsCaptured=all(f['captured'] for f in parts),semanticIdentityVerified=False)],
        processInput=False,ptraceAttach=False,securitySettingsChanged=False,gameMemoryWritten=False,
        artifacts={str(p.relative_to(ROOT)):digest(p.read_bytes()) for p in artifacts})
    out.write_text(json.dumps(result,indent=2)+'\n')
    print(json.dumps(dict(bytesRead=reader.total,records=result['records'])))

if __name__=='__main__':capture()
