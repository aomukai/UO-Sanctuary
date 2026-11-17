#!/usr/bin/env python3
"""Account for active Barbarian input separately from the ten passives."""
import argparse,json
from pathlib import Path
from class_input_coverage import ROOT,collect,sha
OUTPUT=ROOT/'tools/reference/barbarian-current-input-coverage.json'
def review(check=False):
    result=collect('Barbarian',[ROOT/'.local/class-skills/barbarian-lifecycle-acceptance'])
    assert (result['activeTalents'],result['passiveTalents'],result['verifiedActiveInputRoutes'])==(20,10,20)
    result['files'][str(Path(__file__).resolve().relative_to(ROOT))]=sha(Path(__file__))
    text=json.dumps(result,indent=2)+'\n'
    if check:assert OUTPUT.read_text()==text,'Barbarian input audit stale'
    else:OUTPUT.write_text(text)
    print(f"Barbarian:20 active input routes/{result['ordinaryInputCases']} inputs;10 passives remain separate.")
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');review(p.parse_args().check)
