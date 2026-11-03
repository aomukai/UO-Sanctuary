#!/usr/bin/env python3
"""Capture known collision write callees with the existing read-only guards."""
import json
from pathlib import Path
import capture_reference_callees as callees
from probe_warlock_runtime import ROOT,digest

FRESH=ROOT/'.local/class-skills/pet-collision-write-reference-20261103'
ORIGINAL=ROOT/'.local/class-skills/pet-path-commit-reference-20261103/callees-path-commit-footprints-20261103.json'

def prepare():
    original=json.loads(ORIGINAL.read_text());FRESH.mkdir(exist_ok=True)
    for name,value in original['artifacts'].items():assert digest((ROOT/name).read_bytes())==value,name
    rows=[r for r in original['records'] if r['rva'] in ('0x2a5d30','0x2a5fd0')]
    assert len(rows)==2 and all(r['allUnwindFragmentsCaptured'] for r in rows)
    # This direct-call index uses only the two coherent middle fragments.
    # The complete original wrappers and inline tables remain frozen and are
    # already executed/validated by probe_generic_pet_footprints.py.
    files=[ORIGINAL,Path(__file__).resolve()]
    for r in rows:
        fragment=r['fragments'][1];assert fragment['captured'] and fragment['invalidInstructionMarkers']==0
        binary=ORIGINAL.parent/f"callees-path-commit-footprints-20261103-{int(r['rva'],16):x}-part1.bin"
        assert digest(binary.read_bytes())==fragment['sha256']
        asm=binary.with_suffix('.asm');assert '(bad)' not in asm.read_text();files.extend([binary,asm])
    view=dict(referenceVersion='3.3.93847',records=rows,
        derivedView='Direct-call index from coherent middle fragments of complete preserved size wrappers. No table bytes are reinterpreted as code;original complete captures remain unchanged.',
        artifacts={str(p.relative_to(ROOT)):digest(p.read_bytes()) for p in files})
    target=FRESH/'write-callers.json'
    if target.exists():assert json.loads(target.read_text())==view
    else:target.write_text(json.dumps(view,indent=2)+'\n')
    callees.STAGE=FRESH

def capture():
    prepare()
    callees.capture('write-callers.json','pet-collision-writes-20261103',[0x2a6400,0x2a4e90,0x2a61a0,0x2a4ca0])

if __name__=='__main__':capture()
