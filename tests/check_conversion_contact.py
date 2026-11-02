#!/usr/bin/env python3
"""Conversion's distinct successful allegiance and failed ordinary contact paths."""
import os,sys
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_zeal_contact import configuration as zeal

def configuration(full=False):
 c=zeal(full);c['stage']=ROOT/'.local/class-skills/conversion-contact'
 if not full:
  c['extra_families']+=('conversion',)
  c['extra_manifest_names']['conversion']='conversion-checks.json'
  c['extra_wait_results']['conversion-check']='conversion-result'
 c['extra_checks']+=('conversion-contact-check',)
 c['extra_target_input_commands']+=tuple(prefix+mode for prefix in ('conversion-contact-','conversion-allegiance-') for mode in ('left','right','quick'))
 c['extra_inputs']+=(Path(__file__).resolve(),)
 return c
if __name__=='__main__':
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**configuration('--full' in sys.argv))
