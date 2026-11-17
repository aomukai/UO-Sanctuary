#!/usr/bin/env python3
"""Account for Sorceress ordinary and genuinely held input paths."""
import argparse,json
from pathlib import Path
from class_input_coverage import ROOT,collect,sha
OUTPUT=ROOT/'tools/reference/sorceress-current-input-coverage.json'
def review(check=False):
    result=collect('Sorceress',[ROOT/'.local/class-skills/sorceress-lifecycle-acceptance'],negative_prefixes=('buff-payment-','enchant-hostile-'),held=dict(
        talentId='sorceress.inferno',labels=tuple('inferno-held-wire-'+str(n)for n in (0,1,9)),
        ownerSource='tests/Sanctuary.Integration/InfernoHeldInputChecks.cs',guardSource='src/Sanctuary.Content/PlayerInfernoInput.cs',
        ownerAssignment='ClassCreation.Profiles.Assign(p,DiabloClass.Sorceress)',wireOwner='var p=Owner();',availabilityGuard='SorceressCombat.Available(owner,ability)'))
    assert result['activeTalents']==26 and result['passiveTalents']==4 and result['heldInputSequences']==9
    result['files'][str(Path(__file__).resolve().relative_to(ROOT))]=sha(Path(__file__))
    text=json.dumps(result,indent=2)+'\n'
    if check:assert OUTPUT.read_text()==text,'Sorceress input audit stale'
    else:OUTPUT.write_text(text)
    print(f"Sorceress inputs:26 active talents,{result['ordinaryInputCases']} ordinary cases,9held sequences;negative controls and4passives stay separate.")
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');review(p.parse_args().check)
