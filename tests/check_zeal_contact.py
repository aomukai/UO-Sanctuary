#!/usr/bin/env python3
"""Zeal sequence delivery through shared ordered player weapon contact."""
import os,sys
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_vengeance_contact import configuration as vengeance

def configuration(full=False):
 c=vengeance(full);c['stage']=ROOT/'.local/class-skills/zeal-contact'
 if not full:
  c['extra_families']+=('zeal',)
  c['extra_manifest_names']['zeal']='zeal-checks.json'
 c['extra_checks']+=('zeal-contact-check',)
 c['extra_target_input_commands']+=tuple('zeal-contact-'+mode for mode in ('left','right','quick'))
 c['extra_inputs']+=(Path(__file__).resolve(),)
 return c
if __name__=='__main__':
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**configuration('--full' in sys.argv))
