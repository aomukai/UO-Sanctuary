#!/usr/bin/env python3
"""Sacrifice start allocation, retained Paladin mechanics and original saves."""
import os,sys
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_paladin_baseline import configuration as paladin

def configuration(full=False):
 c=paladin(full)
 if not full:
  c.update(extra_families=('sacrifice',),extra_checks=(),extra_wait_results={},extra_files={},extra_manifest_names={'sacrifice':'sacrifice-checks.json'},extra_inputs=())
 c['stage']=ROOT/'.local/class-skills/sacrifice-allocation'
 c['extra_checks']+=('sacrifice-allocation-check',)
 c['extra_target_input_commands']=c.get('extra_target_input_commands',())+tuple('sacrifice-allocation-'+mode for mode in ('left','right','quick'))
 c['extra_inputs']+=(Path(__file__).resolve(),)
 return c
if __name__=='__main__':
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**configuration('--full' in sys.argv))
