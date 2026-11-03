#!/usr/bin/env python3
"""Bounded read-only capture of verified placement hooks and room-link callees."""
import argparse,json
from pathlib import Path
import capture_reference_callees as callees
from probe_warlock_runtime import ROOT,digest
FRESH=ROOT/'.local/class-skills/pet-world-placement-reference-20261103'
PARENTS={
    'hooks':ROOT/'.local/class-skills/generic-pet-live-follow-reference-20261103/callees-follow-warp-placement-20261103.json',
    'links':ROOT/'.local/class-skills/pet-path-commit-reference-20261103/callees-path-commit-leaves-20261103.json',
    'hook-leaves':FRESH/'callees-pet-world-hooks-20261103.json',
    'link-leaves':FRESH/'callees-pet-world-links-20261103.json'
}
TARGETS={'hooks':[0x264460,0x2641b0,0x36f050],'links':[0x2a3c80,0x2a3940],
    'hook-leaves':[0x278110,0x278d30],'link-leaves':[0x2a3e70]}
def capture(part):
    source=PARENTS[part];proof=json.loads(source.read_text())
    assert proof['referenceVersion']=='3.3.93847'
    for name,h in proof['artifacts'].items():assert digest((ROOT/name).read_bytes())==h,name
    FRESH.mkdir(exist_ok=True);parent=FRESH/source.name
    if parent.exists():assert parent.read_bytes()==source.read_bytes()
    else:parent.write_bytes(source.read_bytes())
    callees.STAGE=FRESH
    callees.capture(parent.name,'pet-world-'+part+'-20261103',TARGETS[part])
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('part',choices=list(PARENTS));capture(p.parse_args().part)
