#!/usr/bin/env python3
"""Bounded read-only candidate table/code capture; --check is entirely offline."""
import argparse, json, os, struct
from pathlib import Path
from probe_warlock_runtime import ROOT, Image, CodeReader, identity, digest, disassemble
from pe_unwind_groups import UnwindGroups

STAGE=ROOT/'.local/class-skills/skill-do-dispatch-current'
TABLE=0x163a8b0
SLOTS=191  # Sample length, not a claim about the current dispatcher's bounds.
SELECTED={6:0x4651f0,16:0x467190,49:0x447730}

def capture():
    target=STAGE/'capture.json'
    assert not target.exists(),'Preserve the previous observation'
    STAGE.mkdir(parents=True,exist_ok=True)
    image=Image(); reader=CodeReader(image); artifacts=[]; records=[]
    try:
        reader.anchors()
        size=SLOTS*8; va=image.base+TABLE
        assert image.section(TABLE,size)['name']=='.rdata'
        entry=next(m for m in reader.maps if m[0]<=va and va+size<=m[1])
        assert 'r' in entry[2] and 'w' not in entry[2]
        assert identity(reader.identity['pid'])==reader.identity and reader.total+size*2<=8192
        first=os.pread(reader.fd,size,va); second=os.pread(reader.fd,size,va)
        reader.total+=size*2
        assert first==second==image.bytes(TABLE,size) and identity(reader.identity['pid'])==reader.identity
        reader.reads.append(dict(kind='immutable-skill-do-candidate-table',rva=hex(TABLE),size=size,permissions=entry[2],stable=True,sha256=digest(first)))
        pointers=struct.unpack('<'+str(SLOTS)+'Q',first)
        assert pointers[0]==0 and all(pointers[k]-image.base==v for k,v in SELECTED.items())
        table=STAGE/'candidate-table.bin'; table.write_bytes(first); artifacts.append(table)
        for slot,rva in SELECTED.items():
            record=dict(slot=slot,rva=hex(rva),fragments=[],semanticIdentityVerified=False)
            for i,(begin,end,unwind) in enumerate(UnwindGroups(image).fragments(rva)):
                size=end-begin; address=image.base+begin
                entry=next((m for m in reader.maps if m[0]<=address and image.base+end<=m[1]),None)
                f=dict(rva=hex(begin),endRva=hex(end),unwindRva=hex(unwind),permissions=entry[2]if entry else'cross-map',captured=False)
                if entry and 'r' in entry[2] and 'x' in entry[2] and size<=4096 and reader.total+2*size<=8192:
                    data=reader.stable(begin,size,'code'); binary=STAGE/f'slot-{slot}-part-{i}.bin';binary.write_bytes(data)
                    asm,bad=disassemble(binary,begin,image.base);artifacts += [binary,asm]
                    f.update(captured=True,sha256=digest(data),invalidInstructionMarkers=bad)
                else:f['reason']='Not readable executable within the capture budget; no forced read'
                record['fragments'].append(f)
            record['allUnwindFragmentsCaptured']=all(f['captured']for f in record['fragments']);records.append(record)
    finally:reader.close()
    artifacts += [Path(__file__).resolve(),image.reference,ROOT/'tools/probe_warlock_runtime.py',ROOT/'tools/pe_unwind_groups.py']
    result=dict(referenceVersion='3.3.93847',executableSha256=image.ref['executable']['sha256'],process=reader.identity,bytesRead=reader.total,reads=reader.reads,tableRva=hex(TABLE),sampledSlots=SLOTS,immutableTableMatches=True,dispatchIdentityVerified=False,records=records,processMemoryRead=True,processInput=False,ptraceAttach=False,securitySettingsChanged=False,gameMemoryWritten=False,artifacts={str(p.relative_to(ROOT)):digest(p.read_bytes())for p in artifacts})
    target.write_text(json.dumps(result,indent=2)+'\n')
    print(json.dumps(dict(bytesRead=reader.total,records=records)))

def check():
    result=json.loads((STAGE/'capture.json').read_text());image=Image()
    assert result['executableSha256']==image.ref['executable']['sha256'] and result['bytesRead']<=8192
    assert result['immutableTableMatches'] and not result['dispatchIdentityVerified']
    assert not any(result[k]for k in ['processInput','ptraceAttach','securitySettingsChanged','gameMemoryWritten'])
    assert result['bytesRead']==2*sum(r['size']for r in result['reads'])
    for name,h in result['artifacts'].items():assert digest((ROOT/name).read_bytes())==h,name
    for record in result['records']:
        assert not record['semanticIdentityVerified']
        assert record['allUnwindFragmentsCaptured']==all(f['captured']for f in record['fragments'])
    print('Pinned candidate table/code verified offline; dispatch and semantic identities remain unproven.')

if __name__=='__main__':
    p=argparse.ArgumentParser();g=p.add_mutually_exclusive_group(required=True);g.add_argument('--capture',action='store_true');g.add_argument('--check',action='store_true');a=p.parse_args();capture()if a.capture else check()
