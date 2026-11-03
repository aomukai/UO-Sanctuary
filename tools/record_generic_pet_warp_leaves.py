#!/usr/bin/env python3
"""Preserve fresh warp leaf references without claiming world integration."""
import argparse,json
from pathlib import Path
from probe_warlock_runtime import ROOT,Image,digest
FRESH=ROOT/'.local/class-skills/generic-pet-live-follow-reference-20261103'
def record(check=False):
    image=Image();path=FRESH/'callees-follow-warp-leaves-20261103.json';r=json.loads(path.read_text());artifacts={};seen=set()
    def add(p):
        if p in seen:return
        seen.add(p);artifacts[str(p.relative_to(ROOT))]=digest(p.read_bytes())
        if p.suffix=='.json':
            value=json.loads(p.read_text())
            if isinstance(value,dict):
                for n,h in value.get('artifacts',{}).items():assert digest((ROOT/n).read_bytes())==h,n;add(ROOT/n)
    add(path);add(Path(__file__).resolve());add(image.reference)
    assert r['referenceVersion']=='3.3.93847' and r['executableSha256']==image.ref['executable']['sha256'] and r['directCallTargetsVerified']
    assert r['processMemoryRead'] and r['bytesRead']==1316 and r['bytesRead']==2*sum(x['size'] for x in r['reads'])
    assert not any(r[k] for k in ['processInput','ptraceAttach','securitySettingsChanged','gameMemoryWritten'])
    for x in r['reads']:
        assert x['stable'] and x['size']<=4096 and 'r' in x['permissions']
        assert x['kind']!='code' or 'x' in x['permissions']
    records={}
    for x in r['records']:
        assert x['allUnwindFragmentsCaptured'] and all(f['captured'] and f['invalidInstructionMarkers']==0 for f in x['fragments']);records[x['rva']]=x['bytes']
    assert records=={'0x2a3b30':152,'0x277ff0':138,'0x266860':88}
    result=dict(referenceVersion='3.3.93847',component='Fresh complete warp leaf references',process=r['process'],readOnly=True,processMemoryReadDuringCapture=True,processMemoryReadDuringVerification=False,
        codeBytes=378,bytesRead=1316,records=records,
        sourceReview=['2a3b30checks the unit room,room byte54mask4 and unit word124bit13;on eligibility it sets unit bit13,marks room-owned data and inserts the unit into a room list. It does not reset a path. The policy service is named AfterPlacement.',
            '277ff0returns low16X/Y packed in one32-bit value;dynamic missing paths yield zero,static paths are required.',
            '266860reads a non-tile unit path word at5c. Its complete native geometry interpretation remains separate.'],
        limits=['Complete code availability and source review only;these three leaves have not yet received independent source/native state-machine or actual-world acceptance.',
            'The parent669-byte warp comparison supplies explicit services;synthetic null/unit controls do not prove modern runtime reachability or leaf support.'],artifacts=artifacts)
    out=ROOT/'tools/reference/generic-pet-warp-leaves-source-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert out.read_text()==encoded
    else:out.write_text(encoded)
    print('Warp leaves:3complete bodies/378code bytes,1316bounded read-only bytes;post-placement room-list callback identified;world acceptance remains separate.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
