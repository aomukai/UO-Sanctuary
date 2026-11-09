#!/usr/bin/env python3
"""Bounded read of the two overrides actually indexed by current AI entry."""
import json,re,struct
from probe_warlock_runtime import ROOT,STAGE,Image,CodeReader,digest,disassemble
from pe_unwind_groups import UnwindGroups

def capture():
    parent=STAGE/'pet-ai-event-entry-20261109.json';receipt=json.loads(parent.read_text());row=receipt['records'][0]
    assert row['rva']=='0x43b0c0'and row['allUnwindFragmentsCaptured']
    assembly=[]
    for name,h in receipt['artifacts'].items():
        p=ROOT/name;assert digest(p.read_bytes())==h,name
        if p.suffix=='.asm':assembly.append(p.read_text())
    text='\n'.join(assembly)
    assert '0x141d187f0'in text and re.search(r'cmp\s+ecx,0x1',text)and re.search(r'shl\s+rdi,0x4',text)
    output=STAGE/'pet-ai-overrides-20261109.json';assert not output.exists(),'Preserve earlier reads'
    image=Image();raw=image.bytes(0x1d187f0,32);table=STAGE/'pet-ai-overrides-table-20261109.bin';assert not table.exists();table.write_bytes(raw)
    descriptors=[]
    for i in range(2):
        flags,padding,function=struct.unpack_from('<iIQ',raw,i*16);rva=function-image.base
        assert 0<rva<0x1600000 and function%16==0
        descriptors.append(dict(index=i,flags=flags,padding=padding,function=hex(rva)))
    assert [r['function']for r in descriptors]==['0x4b63d0','0x4b66e0']
    groups=UnwindGroups(image);reader=CodeReader(image);rows=[];artifacts=[parent,table,image.reference]
    try:
        reader.anchors()
        for descriptor in descriptors:
            rva=int(descriptor['function'],16);parts=groups.fragments(rva);record=dict(rva=hex(rva),bytes=sum(b-a for a,b,u in parts),fragments=[])
            for i,(a,b,u)in enumerate(parts):
                va=image.base+a;entry=next((m for m in reader.maps if m[0]<=va and va+b-a<=m[1]),None)
                item=dict(rva=hex(a),endRva=hex(b),unwindRva=hex(u),permissions=entry[2]if entry else 'cross-map',captured=False)
                if entry and 'r'in entry[2]and'x'in entry[2]and b-a<=4096 and reader.total+(b-a)*2<=8192:
                    data=reader.stable(a,b-a,'code');p=STAGE/f'pet-ai-overrides-20261109-{rva:x}-part{i}.bin';p.write_bytes(data);asm,bad=disassemble(p,a,image.base);artifacts.extend([p,asm]);item.update(captured=True,sha256=digest(data),invalidInstructionMarkers=bad)
                else:item['reason']='Not readable executable or outside bounded capture'
                record['fragments'].append(item)
            record['allUnwindFragmentsCaptured']=all(f['captured']for f in record['fragments']);rows.append(record)
    finally:reader.close()
    artifacts.extend([ROOT/'tools/capture_pet_ai_overrides.py',ROOT/'tools/probe_warlock_runtime.py',ROOT/'tools/pe_unwind_groups.py'])
    result=dict(referenceVersion='3.3.93847',executableSha256=image.ref['executable']['sha256'],selection='Current entry43b0c0 indexes two16-byte overrides at1d187f0 for unsigned Field14<=1;table identities qualified,callback mechanics require inspection',tableRva='0x1d187f0',descriptors=descriptors,records=rows,process=reader.identity,processMemoryRead=True,bytesRead=reader.total,reads=reader.reads,processInput=False,ptraceAttach=False,securitySettingsChanged=False,gameMemoryWritten=False,artifacts={str(p.relative_to(ROOT)):digest(p.read_bytes())for p in artifacts})
    output.write_text(json.dumps(result,indent=2)+'\n');print(json.dumps(dict(bytesRead=reader.total,records=[{k:r[k]for k in ['rva','bytes','allUnwindFragmentsCaptured']}for r in rows])))
if __name__=='__main__':capture()
