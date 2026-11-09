#!/usr/bin/env python3
"""Capture two previously uncaptured direct Bind Demon helpers, read-only."""
import json,re
from pathlib import Path
from probe_warlock_runtime import ROOT,STAGE,digest
from capture_reference_callees import capture

def main():
    path=STAGE/'warlock-do-158-20261108/capture.json';parent=json.loads(path.read_text())
    assert parent['referenceVersion']=='3.3.93847'and parent['dispatcherIndexLinked']
    row=next(r for r in parent['records']if r['slot']==158);assert row['rva']=='0x44bc50'and row['allUnwindFragmentsCaptured']
    for name,h in parent['artifacts'].items():assert digest((ROOT/name).read_bytes())==h,name
    calls=set()
    for name in parent['artifacts']:
        p=ROOT/name
        if p.suffix=='.asm'and p.parent==path.parent:
            calls.update(int(v,16)-0x140000000 for v in re.findall(r'call\s+0x([0-9a-f]+)',p.read_text()))
    targets=[0x44b240,0x44b3b0];assert set(targets)<=calls
    alias=STAGE/'bind-demon-parent-20261109.json';assert not alias.exists()
    parent['artifacts'][str(path.relative_to(ROOT))]=digest(path.read_bytes())
    parent['artifacts'][str(Path(__file__).resolve().relative_to(ROOT))]=digest(Path(__file__).read_bytes())
    alias.write_text(json.dumps(parent,indent=2)+'\n')
    capture(alias.name,'bind-demon-core-20261109',targets)
if __name__=='__main__':main()
