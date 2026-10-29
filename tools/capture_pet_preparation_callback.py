#!/usr/bin/env python3
"""Bounded read-only capture of the callback passed by current pet preparation.

Unlike a direct call, this reference is the exact LEA-r8 / iterator-call sequence
in a previously captured, complete parent. It does not permit arbitrary RVAs.
"""
import json,struct
from pathlib import Path
from probe_warlock_runtime import ROOT,STAGE,Image,CodeReader,digest,disassemble
from pe_unwind_groups import UnwindGroups

def capture():
    parentpath=STAGE/'callees-live-actions-execute-20261029.json';parent=json.loads(parentpath.read_text())
    record=next(r for r in parent['records'] if r['rva']=='0x4b2810');assert record['allUnwindFragmentsCaptured']
    fragment=next(f for f in record['fragments'] if int(f['rva'],16)<=0x4b2c01<int(f['endRva'],16))
    paths=[ROOT/n for n,h in parent['artifacts'].items() if n.endswith('.bin') and h==fragment['sha256']];assert len(paths)==1
    source=paths[0];data=source.read_bytes();assert digest(data)==fragment['sha256']
    start=0x4b2c01-int(fragment['rva'],16);sequence=data[start:start+29]
    # R8 is assigned the callback; the intervening instructions only set RDX,
    # RCX and stack arguments before calling the owner iterator.
    assert sequence==bytes.fromhex('4c8d05e8f9ffff488bd188442450488b4c24284c89642440e8424becff')
    target=0x4b2c01+7+struct.unpack('<i',sequence[3:7])[0];iterator=0x4b2c19+5+struct.unpack('<i',sequence[-4:])[0]
    assert target==0x4b25f0 and iterator==0x377760
    label='live-preparation-callback-20261029';out=STAGE/(label+'.json');assert not out.exists(),'Preserve prior capture'
    image=Image();unwind=UnwindGroups(image);reader=CodeReader(image);artifacts=[parentpath,source,Path(__file__).resolve(),ROOT/'tools/probe_warlock_runtime.py',ROOT/'tools/pe_unwind_groups.py'];parts=[]
    try:
        reader.anchors()
        for index,(begin,end,u) in enumerate(unwind.fragments(target)):
            size=end-begin;entry=next((m for m in reader.maps if m[0]<=image.base+begin and image.base+end<=m[1]),None)
            item=dict(rva=hex(begin),endRva=hex(end),unwindRva=hex(u),permissions=entry[2] if entry else 'cross-map',captured=False)
            if entry and 'r' in entry[2] and 'x' in entry[2] and size<=4096 and reader.total+size*2<=8192:
                b=reader.stable(begin,size,'code');p=STAGE/f'{label}-part{index}.bin';p.write_bytes(b);asm,bad=disassemble(p,begin,image.base);artifacts += [p,asm]
                item.update(captured=True,sha256=digest(b),invalidInstructionMarkers=bad)
            else:item['reason']='Not readable executable or outside bounded capture'
            parts.append(item)
    finally:reader.close()
    result=dict(referenceVersion='3.3.93847',executableSha256=image.ref['executable']['sha256'],parent=str(parentpath.relative_to(ROOT)),callbackReference=dict(siteRva='0x4b2c01',callbackRva=hex(target),iteratorRva=hex(iterator),argumentRegister='R8',sequenceSha256=digest(sequence)),process=reader.identity,processMemoryRead=True,bytesRead=reader.total,reads=reader.reads,records=[dict(rva=hex(target),bytes=sum(int(f['endRva'],16)-int(f['rva'],16) for f in parts),fragments=parts,allUnwindFragmentsCaptured=all(f['captured'] for f in parts),semanticIdentityVerified=False)],processInput=False,ptraceAttach=False,securitySettingsChanged=False,gameMemoryWritten=False,artifacts={str(p.relative_to(ROOT)):digest(p.read_bytes()) for p in artifacts})
    out.write_text(json.dumps(result,indent=2)+'\n');print(json.dumps(dict(bytesRead=reader.total,record=result['records'][0])))
if __name__=='__main__':capture()
