#!/usr/bin/env python3
"""Bounded read-only capture of current nearby-search collision callees.

Uses the unchanged same-owner/image/RX/stability/budget guards. Historical
captures remain frozen; this follows only already-preserved direct calls.
"""
import argparse,json
from pathlib import Path
import capture_reference_callees as callees
from probe_warlock_runtime import ROOT,digest

FRESH=ROOT/'.local/class-skills/pet-collision-reference-20261103'
PARENT=ROOT/'.local/class-skills/pet-path-commit-reference-20261103/callees-path-commit-nearby-20261103.json'

def capture(part):
    original=PARENT if part=='checks' else FRESH/'callees-pet-collision-checks-20261103.json'
    parent=json.loads(original.read_text())
    assert parent['referenceVersion']=='3.3.93847'
    for name,value in parent['artifacts'].items():
        assert digest((ROOT/name).read_bytes())==value,name
    FRESH.mkdir(exist_ok=True)
    copied=FRESH/original.name
    if copied.exists():assert copied.read_bytes()==original.read_bytes()
    else:copied.write_bytes(original.read_bytes())
    callees.STAGE=FRESH
    if part=='checks':callees.capture(copied.name,'pet-collision-checks-20261103',[0x2a4920,0x2a5060,0x2a4b30])
    else:callees.capture(copied.name,'pet-collision-leaves-20261103',[0x2a4990,0x2a4a30,0x2972c0])

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('part',choices=['checks','leaves']);capture(p.parse_args().part)
