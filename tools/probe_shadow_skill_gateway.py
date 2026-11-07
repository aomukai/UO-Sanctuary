#!/usr/bin/env python3
"""Capture only immutable callback edges referenced by the captured mode lookup."""
import argparse,json,os,struct
from pathlib import Path
from probe_warlock_runtime import ROOT,STAGE,Image,CodeReader,identity,digest,disassemble
from pe_unwind_groups import UnwindGroups
OUT=STAGE/'shadow-mode-table-20261107.json';TABLE=0x163b5f0
EDGES=[(4,0,0x3dca90),(4,1,0x3dcc70),(14,1,0x3dd530)]
def capture():
    assert not OUT.exists(),'Preserve prior captures'
    parent=STAGE/'services-shadow-mode-row-20261107.json';proof=json.loads(parent.read_text());row=proof['records'][0]
    assert row['rva']=='0x3dd650' and row['allUnwindFragmentsCaptured']
    for p,h in proof['artifacts'].items():assert digest((ROOT/p).read_bytes())==h,p
    assert len(row['fragments'])==1
    binary=next(ROOT/p for p,h in proof['artifacts'].items()if p.endswith('.bin')and h==row['fragments'][0]['sha256']);code=binary.read_bytes()
    # Captured lookup forms table + (mode << 5); no guessed table scan.
    assert code[0xe6:0xe9]==bytes.fromhex('48 8b c7') and code[0xe9:0xec]==bytes.fromhex('48 8d 0d')
    assert 0x3dd650+0xe9+7+struct.unpack_from('<i',code,0xec)[0]==TABLE
    assert code[0xf0:0xf7]==bytes.fromhex('48 c1 e0 05 48 03 c1')
    image=Image();assert image.ref['executable']['sha256']==proof['executableSha256']
    reader=CodeReader(image);records=[];artifacts=[parent,binary,Path(__file__).resolve()]
    try:
        reader.anchors();size=16*32;va=image.base+TABLE
        assert image.section(TABLE,size)['name']=='.rdata'
        entry=next(m for m in reader.maps if m[0]<=va and va+size<=m[1]);assert 'r'in entry[2]and'w'not in entry[2]
        assert identity(reader.identity['pid'])==reader.identity
        first=os.pread(reader.fd,size,va);second=os.pread(reader.fd,size,va);reader.total+=size*2
        assert first==second==image.bytes(TABLE,size) and identity(reader.identity['pid'])==reader.identity
        reader.reads.append(dict(kind='immutable-mode-callback-table',rva=hex(TABLE),size=size,permissions=entry[2],stable=True,sha256=digest(first)))
        p=STAGE/'shadow-mode-table-20261107.bin';p.write_bytes(first);artifacts.append(p)
        for slot,field,rva in EDGES:
            assert struct.unpack_from('<Q',first,slot*32+field*8)[0]==image.base+rva
            record=dict(slot=slot,field=field,rva=hex(rva),fragments=[],semanticIdentityVerified=False)
            for i,(a,b,u)in enumerate(UnwindGroups(image).fragments(rva)):
                size=b-a;entry=next((m for m in reader.maps if m[0]<=image.base+a and image.base+b<=m[1]),None)
                f=dict(rva=hex(a),endRva=hex(b),unwindRva=hex(u),permissions=entry[2]if entry else'cross-map',captured=False)
                if entry and 'r'in entry[2]and'x'in entry[2]and size<=4096 and reader.total+size*2<=8192:
                    data=reader.stable(a,size,'code');p=STAGE/f'shadow-mode-table-{rva:x}-part{i}.bin';p.write_bytes(data);asm,bad=disassemble(p,a,image.base)
                    artifacts += [p,asm];f.update(captured=True,sha256=digest(data),invalidInstructionMarkers=bad)
                else:f['reason']='Not readable executable within bounded capture;no forced read'
                record['fragments'].append(f)
            record['allUnwindFragmentsCaptured']=all(f['captured']for f in record['fragments']);records.append(record)
    finally:reader.close()
    artifacts += [ROOT/'tools/probe_warlock_runtime.py',ROOT/'tools/pe_unwind_groups.py',image.reference]
    result=dict(referenceVersion='3.3.93847',executableSha256=image.ref['executable']['sha256'],sourceCapture=str(parent.relative_to(ROOT)),sourceFunction='0x3dd650',tableRva=hex(TABLE),rowBytes=32,sampledRows=16,immutableTableMatches=True,tableEdgesVerified=True,skillDoDispatchVerified=False,process=reader.identity,bytesRead=reader.total,reads=reader.reads,records=records,processMemoryRead=True,processInput=False,ptraceAttach=False,securitySettingsChanged=False,gameMemoryWritten=False,artifacts={str(p.relative_to(ROOT)):digest(p.read_bytes())for p in artifacts})
    OUT.write_text(json.dumps(result,indent=2)+'\n');print(json.dumps(dict(bytesRead=reader.total,records=[dict(rva=r['rva'],complete=r['allUnwindFragmentsCaptured'])for r in records])))
def check():
    r=json.loads(OUT.read_text());assert r['bytesRead']<=8192 and r['bytesRead']==2*sum(x['size']for x in r['reads'])
    assert r['immutableTableMatches'] and r['tableEdgesVerified'] and not r['skillDoDispatchVerified']
    assert not any(r[k]for k in ['processInput','ptraceAttach','securitySettingsChanged','gameMemoryWritten'])
    for p,h in r['artifacts'].items():assert digest((ROOT/p).read_bytes())==h,p
    assert Image().ref['executable']['sha256']==r['executableSha256']
    print('Current mode-table edges verified offline;skill-do dispatcher and full NPC casting remain separate.')
if __name__=='__main__':
    p=argparse.ArgumentParser();g=p.add_mutually_exclusive_group(required=True);g.add_argument('--capture',action='store_true');g.add_argument('--check',action='store_true');a=p.parse_args();capture()if a.capture else check()
