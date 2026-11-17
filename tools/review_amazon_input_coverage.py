#!/usr/bin/env python3
"""Account for current Amazon class-byte inputs and retained legacy Cast controls."""
import argparse,json,re,sys
from pathlib import Path
from class_input_coverage import ROOT,collect,sha
sys.path.insert(0,str(ROOT/'tests'))
from check_amazon_planned_acceptance import LEGACY_INPUTS
OUTPUT=ROOT/'tools/reference/amazon-current-input-coverage.json'

def review(check=False):
    stage=ROOT/'.local/class-skills/amazon-lifecycle-acceptance';result=collect('Amazon',[stage])
    assert (result['activeTalents'],result['passiveTalents'],result['verifiedActiveInputRoutes'])==(24,6,24)
    assert result['ordinaryInputCases']+result['foreignInputCasesSeparatelyExcluded']==195
    rows=[r for r in json.loads((ROOT/'data/Sanctuary/Balance/class-skills.json').read_text())if r['characterClass']=='Amazon']
    log=(stage/'birth-native-checks.log').read_text();legacy={}
    for label in LEGACY_INPUTS:
        row=next(r for r in rows if r['name'].replace(' ','').lower()==label.replace('-',''))
        observed=re.findall(r' '+re.escape(label)+r'-input: ready ability=(\d+) x=',log)
        assert observed==[str(row['ability'])]*3
        assert len(re.findall(r' '+re.escape(label)+r'-input-check: passed',log))==len(re.findall(r' '+re.escape(label)+r'-input-result: passed',log))==3
        legacy[label]=dict(ability=row['ability'],successfulCases=3)
    assert re.findall(r' decoy-input: ready ability=(\d+) x=',log)==['21']*3
    result.update(legacyCastCases=45,legacyAbilityControls=legacy,legacyLimits='Fourteen logged legacy ability routes and three Decoy Cast/expiry controls omit a class byte;they are counted separately. All24active routes already have current explicit class/ability input evidence. Legacy controls do not add active talents or prove another input protocol.')
    result['files'][str(Path(__file__).resolve().relative_to(ROOT))]=sha(Path(__file__))
    text=json.dumps(result,indent=2)+'\n'
    if check:assert OUTPUT.read_text()==text,'Amazon input audit stale'
    else:OUTPUT.write_text(text)
    print(f"Amazon:24active routes/{result['ordinaryInputCases']} explicit-class inputs;45legacy controls/{result['foreignInputCasesSeparatelyExcluded']} foreign inputs separate;6passives.")

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');review(p.parse_args().check)
