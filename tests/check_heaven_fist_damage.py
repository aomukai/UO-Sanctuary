#!/usr/bin/env python3
"""FoH primary typed delayed receiver and original paid source persistence."""
import os,sys
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_blessed_hammer_flight import configuration as hammer

def configuration(full=False):
 c=hammer(full);c['stage']=ROOT/'.local/class-skills/heaven-fist-contact'
 c['extra_families']+=('heaven-fist-damage',)
 c['extra_manifest_names']['heaven-fist-damage']='heaven-fist-damage-checks.json'
 c['extra_wait_results']['heaven-fist-damage-reload-check']='heaven-fist-damage-reload-result'
 c['extra_connected_checks']=c.get('extra_connected_checks',())+('heaven-fist-damage-reload-check',)
 c['extra_target_input_commands']+=tuple('heaven-fist-damage-'+mode for mode in ('left','right','quick'))
 c['extra_inputs']+=(Path(__file__).resolve(),ROOT/'tools/probe_heaven_fist_delay.py',ROOT/'tools/reference/heaven-fist-delay-engine-evidence.json',ROOT/'.local/class-skills/heaven-fist-contact/delay-source/cases.json')
 return c
if __name__=='__main__':
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**configuration('--full' in sys.argv))
