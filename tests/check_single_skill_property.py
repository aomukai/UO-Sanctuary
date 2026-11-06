#!/usr/bin/env python3
"""Original single-skill layers through pet equipment, contact and two reloads."""
import argparse,os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_saved_item_ethereality import configuration as prior
def configuration(retained=False):
 c=prior(True)if retained else dict(extra_families=(),extra_inputs=(),extra_connected_checks=(),extra_files={})
 c['stage']=ROOT/'.local/class-skills'/('single-skill-property-retained'if retained else'single-skill-property')
 c['extra_families']+=('single-skill-property',)
 c['extra_connected_checks']+=('single-skill-property-check','single-skill-property-reload-check')
 c['extra_files']['single-skill-property-cases.json']=ROOT/'.local/class-skills/single-skill-property-source/cases.json'
 c['extra_inputs']+=(Path(__file__).resolve(),)
 return c
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--retained',action='store_true');a=p.parse_args()
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**configuration(a.retained))
