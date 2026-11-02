#!/usr/bin/env python3
"""Holy Bolt typed spell receiver,approved potion and saved capture."""
import os,sys
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_charge_contact import configuration as charge

def configuration(full=False):
 c=charge(full);c['stage']=ROOT/'.local/class-skills/holy-bolt-damage'
 if not full:
  c['extra_families']+=('holy-bolt','heaven-fist')
  c['extra_manifest_names'].update({'holy-bolt':'holy-bolt-checks.json','heaven-fist':'heaven-fist-checks.json'})
  c['extra_wait_results'].update({'holy-bolt-check':'holy-bolt-result','holy-bolt-reload-check':'holy-bolt-reload-result','heaven-fist-check':'heaven-fist-result','heaven-fist-reload-check':'heaven-fist-reload-result'})
 c['extra_families']+=('holy-bolt-damage',)
 c['extra_manifest_names']['holy-bolt-damage']='holy-bolt-damage-checks.json'
 c['extra_target_input_commands']+=tuple('holy-bolt-damage-'+mode for mode in ('left','right','quick'))
 c['extra_inputs']+=(Path(__file__).resolve(),)
 return c
if __name__=='__main__':
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**configuration('--full' in sys.argv))
