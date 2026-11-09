#!/usr/bin/env python3
"""Read only qualified direct AI-entry callees,excluding its explicit inline data."""
import argparse,json,re,struct
from probe_warlock_runtime import ROOT,STAGE,Image,CodeReader,digest,disassemble
from pe_unwind_groups import UnwindGroups

def capture(label,rvas):
    assert re.fullmatch('[a-z0-9-]+',label)
    parent=STAGE/'pet-ai-event-entry-20261109.json';receipt=json.loads(parent.read_text());row=receipt['records'][0]
    assert row['rva']=='0x43b0c0' and row['allUnwindFragmentsCaptured']
    calls=set();table=None
    for name,h in receipt['artifacts'].items():
        p=ROOT/name;assert digest(p.read_bytes())==h,name
        if p.suffix=='.bin' and '43b0c0-part4' in p.name:
            begin=int(row['fragments'][4]['rva'],16);raw=p.read_bytes();table=raw[0x43b4ec-begin:0x43b508-begin]
        if p.suffix=='.asm':
            for line in p.read_text().splitlines():
                match=re.match(r'\s*([0-9a-f]+):',line)
                if match and int(match[1],16)<0x14043b4ec:
                    assert '(bad)'not in line,'Incoherent executable instruction'
                    calls.update(int(v,16)-0x140000000 for v in re.findall(r'call\s+0x([0-9a-f]+)',line))
    assert table is not None and struct.unpack('<7I',table)==(0x43b30f,0x43b21f,0x43b298,0x43b21f,0x43b25e,0x43b2be,0x43b2e7)
    assert set(rvas)<=calls,'Not a captured direct callee'
    output=STAGE/('callees-'+label+'.json');assert not output.exists(),'Preserve earlier reads'
    image=Image();groups=UnwindGroups(image);reader=CodeReader(image);rows=[];artifacts=[]
    try:
        reader.anchors()
        for rva in rvas:
            parts=groups.fragments(rva);record=dict(rva=hex(rva),bytes=sum(b-a for a,b,u in parts),fragments=[])
            for i,(a,b,u) in enumerate(parts):
                va=image.base+a;entry=next((m for m in reader.maps if m[0]<=va and va+b-a<=m[1]),None)
                item=dict(rva=hex(a),endRva=hex(b),unwindRva=hex(u),permissions=entry[2]if entry else 'cross-map',captured=False)
                if entry and 'r'in entry[2]and'x'in entry[2]and b-a<=4096 and reader.total+(b-a)*2<=8192:
                    raw=reader.stable(a,b-a,'code');p=STAGE/f'callees-{label}-{rva:x}-part{i}.bin';p.write_bytes(raw);asm,bad=disassemble(p,a,image.base);artifacts.extend([p,asm]);item.update(captured=True,sha256=digest(raw),invalidInstructionMarkers=bad)
                else:item['reason']='Not readable executable or outside bounded capture'
                record['fragments'].append(item)
            record['allUnwindFragmentsCaptured']=all(f['captured']for f in record['fragments']);rows.append(record)
    finally:reader.close()
    artifacts.extend([parent,ROOT/'tools/capture_pet_ai_entry_services.py',ROOT/'tools/probe_warlock_runtime.py',ROOT/'tools/pe_unwind_groups.py'])
    result=dict(referenceVersion='3.3.93847',executableSha256=image.ref['executable']['sha256'],parent=str(parent.relative_to(ROOT)),directCallTargetsVerified=True,inlineData=dict(rva='0x43b4ec',bytes=28,sha256=digest(table),targets=[hex(v)for v in struct.unpack('<7I',table)],executedAsCode=False),records=rows,process=reader.identity,processMemoryRead=True,bytesRead=reader.total,reads=reader.reads,processInput=False,ptraceAttach=False,securitySettingsChanged=False,gameMemoryWritten=False,artifacts={str(p.relative_to(ROOT)):digest(p.read_bytes())for p in artifacts})
    output.write_text(json.dumps(result,indent=2)+'\n');print(json.dumps(dict(bytesRead=reader.total,records=[{k:r[k]for k in ['rva','bytes','allUnwindFragmentsCaptured']}for r in rows])))
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--label',required=True);p.add_argument('--rvas',type=lambda v:int(v,0),nargs='+',required=True);a=p.parse_args();capture(a.label,a.rvas)
