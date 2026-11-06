#!/usr/bin/env python3
"""SQ14 and absolute phase-reset scheduler with prior learned-class controls."""
import argparse,os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_grim_ward_actor import configuration as prior
def configuration(retained=False):
 c=prior(True)if retained else dict(extra_families=(),extra_checks=(),extra_files={},extra_inputs=())
 c['stage']=ROOT/'.local/class-skills'/('leap-attack-sequence-retained'if retained else'leap-attack-sequence')
 c['extra_checks']+=('leap-attack-sequence-check',)
 if not retained:
  c['extra_families']+=('double-swing-sequence',)
  c['extra_files']['double-swing-sequence-cases.json']=ROOT/'.local/class-skills/double-swing-sequence-source/cases.json'
 c['extra_files']['leap-attack-sequence-cases.json']=ROOT/'.local/class-skills/leap-attack-sequence-source/cases.json'
 c['extra_inputs']+=(Path(__file__).resolve(),)
 return c
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--retained',action='store_true');a=p.parse_args()
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**configuration(a.retained))
