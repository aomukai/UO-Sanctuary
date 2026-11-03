#!/usr/bin/env python3
"""Verify newly captured search/placement/candidate bodies without a world claim."""
import argparse,json
from pathlib import Path
from probe_warlock_runtime import ROOT,Image,digest
FRESH=ROOT/'.local/class-skills/generic-pet-live-follow-reference-20261103'
def record(check=False):
    image=Image();artifacts={};seen=set();records={};captures=[];identities=[]
    def add(p):
        if p in seen:return
        seen.add(p);artifacts[str(p.relative_to(ROOT))]=digest(p.read_bytes())
        if p.suffix=='.json':
            value=json.loads(p.read_text())
            if isinstance(value,dict):
                for n,h in value.get('artifacts',{}).items():assert digest((ROOT/n).read_bytes())==h,n;add(ROOT/n)
    for name in ['callees-follow-warp-placement-20261103.json','callees-follow-warp-candidate-20261103.json']:
        path=FRESH/name;r=json.loads(path.read_text());add(path)
        assert r['referenceVersion']=='3.3.93847' and r['executableSha256']==image.ref['executable']['sha256'] and r['directCallTargetsVerified']
        assert r['processMemoryRead'] and r['bytesRead']<=8192 and r['bytesRead']==2*sum(x['size'] for x in r['reads'])
        assert not any(r[k] for k in ['processInput','ptraceAttach','securitySettingsChanged','gameMemoryWritten'])
        for x in r['reads']:assert x['stable'] and x['size']<=4096 and 'r' in x['permissions'] and (x['kind']!='code' or 'x' in x['permissions'])
        for x in r['records']:
            assert x['allUnwindFragmentsCaptured'] and all(f['captured'] and f['invalidInstructionMarkers']==0 for f in x['fragments']);records[x['rva']]=x['bytes']
        identities.append(r['process']);captures.append(dict(path=str(path.relative_to(ROOT)),bytesRead=r['bytesRead']))
    assert all(i==identities[0] for i in identities)
    assert records=={'0x358f20':1065,'0x3739a0':1128,'0x428b20':1860} and sum(c['bytesRead'] for c in captures)==9226
    candidate=(FRESH/'callees-follow-warp-candidate-20261103-428b20-part0.bin').read_bytes();assert len(candidate)==1860
    assert candidate[0x42908b-0x428b20:0x429095-0x428b20]==bytes.fromhex('41 f6 c4 01 0f 85 9b 01 00 00')
    add(Path(__file__).resolve());add(image.reference)
    result=dict(referenceVersion='3.3.93847',component='Fresh current search/placement/candidate references',process=identities[0],readOnly=True,processMemoryReadDuringCapture=True,processMemoryReadDuringVerification=False,
        codeBytes=4053,bytesRead=9226,maximumBytesPerCapture=4946,records=records,captures=captures,
        sourceReview=['Warp search uses its room RNG and at most20attempts. The actual warp caller supplies restriction arg7=0.',
            'The actual candidate request word48=1 selects the test at42908b and its jump to429230 before the unit allocation call;this search request does not create an extra summon.'],
        limits=['Complete code and guarded candidate-mode review only. Candidate collision/geometry,full placement/player paths and actual native world binding remain unverified.'],artifacts=artifacts)
    out=ROOT/'tools/reference/generic-pet-warp-search-reference-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert out.read_text()==encoded
    else:out.write_text(encoded)
    print('Warp search references:3complete bodies/4053code bytes,9226bounded read-only bytes across2captures;candidate validation flag guard preserved;world/candidate policy still unverified.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
