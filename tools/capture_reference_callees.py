#!/usr/bin/env python3
"""Capture bounded direct callees of a preserved current-code reference only."""
import argparse,json,re
from pathlib import Path
from probe_warlock_runtime import ROOT,STAGE,Image,CodeReader,digest,disassemble
from pe_unwind_groups import UnwindGroups

def capture(parent_name,label,rvas):
    assert re.fullmatch('[a-z0-9-]+',label)
    assert re.fullmatch('[a-z0-9-]+[.]json',parent_name)
    parentpath=STAGE/parent_name;parent=json.loads(parentpath.read_text());target=STAGE/('callees-'+label+'.json');assert not target.exists(),'Preserve prior captures'
    records=parent.get('records',[])
    assert parent.get('allUnwindFragmentsCaptured') or records and all(r.get('allUnwindFragmentsCaptured') for r in records),'Incomplete parent'
    calls=set();parents=[]
    for name,h in parent['artifacts'].items():
        p=ROOT/name;assert digest(p.read_bytes())==h,name
        if p.suffix=='.asm':
            assert '(bad)' not in p.read_text(),'Incoherent parent disassembly'
            calls.update(int(v,16)-0x140000000 for v in re.findall(r'call\s+0x([0-9a-f]+)',p.read_text()));parents.append(p)
    assert set(rvas)<=calls,'Requested address is not a preserved direct callee'
    image=Image();unwind=UnwindGroups(image);reader=CodeReader(image);output=[];artifacts=[]
    try:
        reader.anchors()
        for rva in rvas:
            parts=unwind.fragments(rva);record=dict(rva=hex(rva),bytes=sum(b-a for a,b,u in parts),fragments=[],semanticIdentityVerified=False)
            for index,(begin,end,u) in enumerate(parts):
                va=image.base+begin;size=end-begin;entry=next((m for m in reader.maps if m[0]<=va and va+size<=m[1]),None)
                item=dict(rva=hex(begin),endRva=hex(end),unwindRva=hex(u),permissions=entry[2] if entry else 'cross-map',captured=False)
                if entry and 'r' in entry[2] and 'x' in entry[2] and size<=4096 and reader.total+size*2<=8192:
                    data=reader.stable(begin,size,'code');p=STAGE/f'callees-{label}-{rva:x}-part{index}.bin';p.write_bytes(data);asm,bad=disassemble(p,begin,image.base);artifacts += [p,asm]
                    item.update(captured=True,sha256=digest(data),invalidInstructionMarkers=bad)
                else:item['reason']='Not readable executable or outside this bounded capture'
                record['fragments'].append(item)
            record['allUnwindFragmentsCaptured']=all(p['captured'] for p in record['fragments']);output.append(record)
    finally:reader.close()
    artifacts += [parentpath,Path(__file__).resolve(),ROOT/'tools/probe_warlock_runtime.py',ROOT/'tools/pe_unwind_groups.py']
    result=dict(referenceVersion='3.3.93847',executableSha256=image.ref['executable']['sha256'],parent=str(parentpath.relative_to(ROOT)),directCallTargetsVerified=True,process=reader.identity,processMemoryRead=True,bytesRead=reader.total,reads=reader.reads,records=output,processInput=False,ptraceAttach=False,securitySettingsChanged=False,gameMemoryWritten=False,artifacts={str(p.relative_to(ROOT)):digest(p.read_bytes()) for p in artifacts})
    target.write_text(json.dumps(result,indent=2)+'\n');print(json.dumps(dict(bytesRead=reader.total,records=[{k:r[k] for k in ['rva','bytes','allUnwindFragmentsCaptured']} for r in output])))
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--parent',required=True);p.add_argument('--label',required=True);p.add_argument('--rvas',type=lambda x:int(x,0),nargs='+',required=True);a=p.parse_args();capture(a.parent,a.label,a.rvas)
