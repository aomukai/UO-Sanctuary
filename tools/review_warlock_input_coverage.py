#!/usr/bin/env python3
"""Actual Warlock/Witch class inputs and independent held Bind sequences."""
import argparse,json
from pathlib import Path
from class_input_coverage import ROOT,collect,sha
OUTPUT=ROOT/'tools/reference/warlock-current-input-coverage.json'

def review(check=False):
    stage=ROOT/'.local/class-skills/warlock-lifecycle-acceptance'
    result=collect('Warlock',[stage],held=dict(talentId='warlock.binddemon',labels=('bind-learned-wire-1','bind-learned-wire-9'),
        ownerSource='tests/Sanctuary.Integration/BindDemonLearnedChecks.cs',guardSource='src/Sanctuary.Content/BindDemonInput.cs',
        ownerAssignment='ClassCreation.Profiles.Assign(p,DiabloClass.Warlock)',wireOwner='var p=Fresh();p.Female=source==9;',
        availabilityGuard='profile.Class!=DiabloClass.Warlock',readyPattern=r': ready ability=(\d+) target=\d+ other=\d+ x=',
        phases=('pressed-free','released-free','pressed','bound'),
        evidence='actual held Bind ability/free-start/free-release/new-press/bound/finish result;class from compiled original owner and binding/class guard'))
    assert(result['activeTalents'],result['passiveTalents'],result['verifiedActiveInputRoutes'],result['heldInputSequences'])==(26,4,26,6)
    result.update(heldLimits='Six actual right/quick Bind sequences include free early release,fresh windup,pinned target,mana and successful original native binding. Source9 uses the female Witch owner. Held packets omit a class byte;compiled ownership and ordinary class/binding/hard-training guards establish context. Repeated heartbeats are not extra casts.',passiveLimits='Demonic Mastery,Blood Oath,Levitation Mastery and Enhanced Entropy retain their separate learned/stat/equipment/summon/recipient consumers;no active binding is invented.')
    result['files'][str(Path(__file__).resolve().relative_to(ROOT))]=sha(Path(__file__))
    text=json.dumps(result,indent=2)+'\n'
    if check:assert OUTPUT.read_text()==text,'Warlock input audit stale'
    else:OUTPUT.write_text(text)
    print(f"Warlock/Witch:26active routes/{result['ordinaryInputCases']} explicit-class inputs/6held Bind;4passives separate.")

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');review(p.parse_args().check)
