#!/usr/bin/env python3
"""Verify fresh read-only follow-reference captures offline, including warp."""
import argparse,json
from pathlib import Path
from probe_warlock_runtime import ROOT,Image,digest
FRESH=ROOT/'.local/class-skills/generic-pet-live-follow-reference-20261103'
def record(check=False):
    image=Image();artifacts={};visited=set()
    def add(path):
        if path in visited:return
        visited.add(path);artifacts[str(path.relative_to(ROOT))]=digest(path.read_bytes())
        if path.suffix=='.json':
            value=json.loads(path.read_text())
            if isinstance(value,dict):
                for name,sha in value.get('artifacts',{}).items():
                    p=ROOT/name;assert digest(p.read_bytes())==sha,name;add(p)
    captures=[];identities=[];records={};total=0
    for name in ['callees-follow-movement-20261103.json','callees-follow-monster-mode-20261103.json','callees-follow-warp-20261103.json']:
        path=FRESH/name;capture=json.loads(path.read_text());add(path)
        assert capture['referenceVersion']=='3.3.93847' and capture['executableSha256']==image.ref['executable']['sha256']
        assert capture['directCallTargetsVerified'] and capture['processMemoryRead'] and not any(capture[k] for k in ['processInput','ptraceAttach','securitySettingsChanged','gameMemoryWritten'])
        assert capture['bytesRead']<=8192 and capture['bytesRead']==2*sum(r['size'] for r in capture['reads'])
        for r in capture['reads']:
            assert r['stable'] and r['size']<=4096 and 'r' in r['permissions']
            assert (r['kind']=='code' and 'x' in r['permissions']) or (r['kind'],r['rva'],r['size']) in [('header','0x0',64),('sequence-table','0x162ba30',216)]
        for r in capture['records']:
            assert r['allUnwindFragmentsCaptured'] and all(f['captured'] and f['invalidInstructionMarkers']==0 for f in r['fragments']);records[r['rva']]=r['bytes']
        identities.append(capture['process']);total+=capture['bytesRead'];captures.append(dict(path=str(path.relative_to(ROOT)),bytesRead=capture['bytesRead']))
    assert all(i==identities[0] for i in identities) and total==4272
    assert records=={'0x425bb0':227,'0x373f60':253,'0x3daa80':147,'0x40e7d0':669}
    add(Path(__file__).resolve());add(image.reference)
    result=dict(referenceVersion='3.3.93847',executableSha256=image.ref['executable']['sha256'],process=identities[0],readOnly=True,
        processMemoryReadDuringCapture=True,processMemoryReadDuringVerification=False,processInput=False,ptraceAttach=False,securitySettingsChanged=False,gameMemoryWritten=False,targetProcessCodeExecuted=False,
        completeCodeBytes=sum(records.values()),totalBytesRead=total,records=records,captures=captures,
        limits=['Direct-call identity,complete captured code and pinned image anchors only. Complete code availability does not itself prove gameplay or native-world equivalence.',
            'Warp body is preserved for subsequent source/native policy comparison. Movement/animation timing,world/path/warp services,cfg+0x70 producer and learned lifecycle remain separate work.'],artifacts=artifacts)
    out=ROOT/'tools/reference/generic-pet-live-follow-source-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert out.read_text()==encoded
    else:out.write_text(encoded)
    print('Fresh follow references:4complete helpers/1296code bytes,4272bounded read-only bytes;offline integrity verified;warp gameplay still unverified.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
