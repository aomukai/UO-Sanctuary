#!/usr/bin/env python3
"""Whole Necromancer uniques grant real zero-hard casts and retain their state."""
import argparse,os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_single_skill_property import configuration as prior
def configuration(retained=False):
 c=prior(True)if retained else dict(extra_families=(),extra_inputs=(),extra_connected_checks=(),extra_files={})
 c['stage']=ROOT/'.local/class-skills'/('necromancer-item-skills-retained'if retained else'necromancer-item-skills')
 c['extra_families']+=('necromancer-item-skill',)
 c['extra_connected_checks']+=('necromancer-item-skill-check','necromancer-item-skill-reload-check')
 c['extra_inputs']+=(Path(__file__).resolve(),)
 return c
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--retained',action='store_true');a=p.parse_args()
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**configuration(a.retained))
