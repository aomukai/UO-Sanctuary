#!/usr/bin/env python3
"""Publish the two table-qualified current AI overrides,without birth defaults."""
import argparse,json,struct
from pathlib import Path
from probe_warlock_runtime import ROOT,STAGE,Image,digest

def build(check=False):
    capture=STAGE/'pet-ai-overrides-20261109.json';e=json.loads(capture.read_text())
    for n,h in e['artifacts'].items():assert digest((ROOT/n).read_bytes())==h,n
    image=Image();raw=image.bytes(0x1d187f0,32);assert raw==(STAGE/'pet-ai-overrides-table-20261109.bin').read_bytes()
    rows=[]
    for i,rva in enumerate([0x4b63d0,0x4b66e0]):
        assert struct.unpack_from('<iIQ',raw,i*16)==(0,0,image.base+rva);rows.append(dict(flags=0,function=rva))
    path=ROOT/'data/Sanctuary/Balance/generic-pet-ai-overrides.json';text=json.dumps(rows,indent=2)+'\n'
    if check:assert path.read_text()==text
    else:path.write_text(text)
    result=dict(referenceVersion='3.3.93847',tableRva='0x1d187f0',stride=16,rows=2,controlProducerVerified=False,birthDefaultsPublished=False,processMemoryRead=False,processInput=False,artifacts={str(p.relative_to(ROOT)):digest(p.read_bytes())for p in [capture,STAGE/'pet-ai-overrides-table-20261109.bin',image.reference,path,Path(__file__).resolve()]})
    path=ROOT/'tools/reference/pet-ai-overrides-data-evidence.json';text=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==text
    else:path.write_text(text)
    print('Two current AI override rows published;no startup/control defaults.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
