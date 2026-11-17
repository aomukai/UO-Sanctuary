#!/usr/bin/env python3
"""Account for current Paladin input without treating foreign casts as coverage."""
import argparse,json
from pathlib import Path
from class_input_coverage import ROOT,collect,sha
OUTPUT=ROOT/'tools/reference/paladin-current-input-coverage.json'

def review(check=False):
    result=collect('Paladin',[ROOT/'.local/class-skills/paladin-lifecycle-acceptance'])
    assert (result['activeTalents'],result['passiveTalents'],result['verifiedActiveInputRoutes'])==(30,0,30)
    result['files'][str(Path(__file__).resolve().relative_to(ROOT))]=sha(Path(__file__))
    text=json.dumps(result,indent=2)+'\n'
    if check:assert OUTPUT.read_text()==text,'Paladin input audit stale'
    else:OUTPUT.write_text(text)
    print(f"Paladin inputs:30 active talents,{result['ordinaryInputCases']} class input cases;foreign casts excluded.")

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');review(p.parse_args().check)
