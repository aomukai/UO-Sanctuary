#!/usr/bin/env python3
"""Blessed Hammer typed magic/capture,retaining its distinct spiral cohort."""
import os,sys
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_holy_bolt_flight import configuration as holy

def configuration(full=False):
 c=holy(full);c['stage']=ROOT/'.local/class-skills/blessed-hammer-contact'
 if not full:
  c['extra_families']+=('blessed-hammer',)
  c['extra_manifest_names']['blessed-hammer']='blessed-hammer-checks.json'
  c['extra_wait_results'].update({'blessed-hammer-check':'blessed-hammer-result','blessed-hammer-reload-check':'blessed-hammer-reload-result'})
 c['extra_families']+=('blessed-hammer-damage',)
 c['extra_manifest_names']['blessed-hammer-damage']='blessed-hammer-damage-checks.json'
 c['extra_target_input_commands']+=tuple('blessed-hammer-damage-'+mode for mode in ('left','right','quick'))
 c['extra_inputs']+=(Path(__file__).resolve(),ROOT/'tools/reference/blessed-hammer-damage-engine-evidence.json',ROOT/'.local/class-skills/blessed-hammer-contact/damage-source/cases.json')
 return c
if __name__=='__main__':
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**configuration('--full' in sys.argv))
