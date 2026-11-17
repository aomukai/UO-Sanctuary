#!/usr/bin/env python3
"""Account separately for Druid class-byte, legacy elemental and held Arctic input."""
import argparse,json,re
from pathlib import Path
from class_input_coverage import ROOT,collect,sha
OUTPUT=ROOT/'tools/reference/druid-current-input-coverage.json'
ELEMENTS=(('firestorm','Firestorm'),('molten-boulder','MoltenBoulder'),('fissure','Fissure'),
          ('cyclone-armor','CycloneArmor'),('twister','Twister'),('volcano','Volcano'),
          ('tornado','Tornado'),('hurricane','Hurricane'),('armageddon','Armageddon'))

def review(check=False):
    stage=ROOT/'.local/class-skills/druid-lifecycle-acceptance';legacy=[]
    for label,fixture in ELEMENTS:
        path=f'tests/Sanctuary.Integration/{fixture}Checks.cs';source=(ROOT/path).read_text()
        assignment=re.search(r'ClassCreation\.Profiles\.Assign\(p,\s*DiabloClass.Druid\);',source).group(0)
        legacy.append(dict(label=label+'-input',talentId='druid.'+fixture.lower(),ownerSource=path,
            ownerFragments=['private static PlayerMobile Fresh()',assignment]))
    basics=tuple('druid-basic-proc-'+form+'-'+side for form in ('wolf','bear')for side in ('left','right'))
    basics+=('basic-melee-druid-left','basic-melee-druid-right')
    result=collect('Druid',[stage],legacy=legacy,basic_controls=basics,held=dict(
        talentId='druid.arcticblast',labels=tuple('arctic-held-wire-'+str(n)for n in (0,1,9)),
        ownerSource='tests/Sanctuary.Integration/ArcticHeldInputChecks.cs',guardSource='src/Sanctuary.Content/PlayerInfernoInput.cs',
        ownerAssignment='ClassCreation.Profiles.Assign(p,DiabloClass.Druid)',wireOwner='var p=Owner();',availabilityGuard='DruidCombat.Available(owner,ability)'))
    assert(result['activeTalents'],result['passiveTalents'],result['verifiedActiveInputRoutes'],result['heldInputSequences'],result['legacyInputSequences'])==(29,1,29,9,27)
    result.update(legacyLimits='Nine actual legacy elemental Cast scenarios per boot omit a class byte;compiled original fixture ownership plus observed ability/check/result establish context. Cyclone Armor also has explicit-class input. These27sequences are separate from ordinary inputs and nine held Arctic sequences;heartbeats and save stages are not new talent routes.')
    result['files'][str(Path(__file__).resolve().relative_to(ROOT))]=sha(Path(__file__))
    text=json.dumps(result,indent=2)+'\n'
    if check:assert OUTPUT.read_text()==text,'Druid input audit stale'
    else:OUTPUT.write_text(text)
    print(f"Druid:29active routes/{result['ordinaryInputCases']} explicit-class inputs/27legacy elemental sequences/9held Arctic;Lycanthropy separate.")

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');review(p.parse_args().check)
