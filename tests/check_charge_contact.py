#!/usr/bin/env python3
"""Charge travel followed by ordered ordinary contact and source knockback."""
import os,sys
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_conversion_contact import configuration as conversion

def configuration(full=False):
 c=conversion(full);c['stage']=ROOT/'.local/class-skills/charge-contact'
 if not full:
  c['extra_families']+=('charge',)
  c['extra_manifest_names']['charge']='charge-checks.json'
 c['extra_checks']+=('charge-contact-check',)
 ground=tuple('charge-ground-'+mode for mode in ('left','right','quick'))
 c['extra_target_input_commands']+=tuple('charge-contact-'+mode for mode in ('left','right','quick'))+ground
 c['extra_ground_input_commands']=c.get('extra_ground_input_commands',())+ground
 c['extra_inputs']+=(Path(__file__).resolve(),)
 return c
if __name__=='__main__':
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**configuration('--full' in sys.argv))
