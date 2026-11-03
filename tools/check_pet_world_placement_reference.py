#!/usr/bin/env python3
"""Check preserved current placement-hook provenance offline;not an oracle."""
import argparse,json
from pathlib import Path
from probe_warlock_runtime import ROOT,digest,Image
FRESH=ROOT/'.local/class-skills/pet-world-placement-reference-20261103'
def check(compare=False):
    image=Image();artifacts=[Path(__file__).resolve(),ROOT/'tools/capture_pet_world_placement.py',image.reference];records=[];total=0;identity=None
    for part in ('hooks','links','hook-leaves','link-leaves'):
        path=FRESH/f'callees-pet-world-{part}-20261103.json';proof=json.loads(path.read_text());artifacts.append(path)
        assert proof['referenceVersion']=='3.3.93847' and proof['executableSha256']==image.ref['executable']['sha256'] and proof['directCallTargetsVerified']
        assert proof['bytesRead']<=8192 and all(r['stable'] and 'r' in r['permissions'] and (r['kind']!='code' or 'x' in r['permissions']) for r in proof['reads'])
        assert not any(proof[k] for k in ('processInput','ptraceAttach','securitySettingsChanged','gameMemoryWritten'))
        if identity is None:identity=proof['process']
        assert identity==proof['process'];total+=proof['bytesRead']
        for name,h in proof['artifacts'].items():
            p=ROOT/name;assert digest(p.read_bytes())==h,name;artifacts.append(p)
        for row in proof['records']:
            assert row['allUnwindFragmentsCaptured'] and all(f['captured'] and f['invalidInstructionMarkers']==0 for f in row['fragments']);records.append([row['rva'],row['bytes']])
    assert total==11052 and sum(n for r,n in records)==4406 and len(records)==8
    mask=(FRESH/'callees-pet-world-hook-leaves-20261103-278110-part0.bin').read_bytes()
    changed=(FRESH/'callees-pet-world-hook-leaves-20261103-278d30-part0.bin').read_bytes()
    assert mask[0x34:0x37]==b'\x8b\x40\x60' and changed[0x2b:0x31]==b'\x8b\x40\x48\x83\xe0\x02'
    result=dict(referenceVersion='3.3.93847',component='Current placement hooks and room-list code reviewed for the native ground adapter',
        process=identity,routineRecords=records,routineBytes=4406,totalReadBytes=11052,maximumBytesPerCapture=6382,
        processMemoryReadDuringCheck=False,machineCodeExecuted=False,mechanicalParityVerified=False,handlerEnabled=False,
        scope=['Provenance and complete readable code only;this is not a new machine-code differential certificate.',
            'The native adapter binds the forced monster before/after size-footprint hooks and operation-local room/update membership. Ground geometry reuses PetGroundWorld;native MoveToWorld supplies UO sector/client transport.',
            'Exact D2 map/crowd geometry,full room-link edge-case comparison and D2 client visibility/global room lists remain unfinished. Automatic GenericPet AI and learned cast stay gated.'],
        artifacts={str(p.relative_to(ROOT)):digest(p.read_bytes()) for p in artifacts})
    target=ROOT/'tools/reference/pet-world-placement-reference-evidence.json'
    if compare:assert json.loads(target.read_text())==result
    else:target.write_text(json.dumps(result,indent=2)+'\n')
    print('Current placement reference:8complete reviewed routines/4406bytes;bounded read-only provenance verified offline;new raw-code differential acceptance remains open.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');check(p.parse_args().check)
