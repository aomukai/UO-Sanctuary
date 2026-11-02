#!/usr/bin/env python3
"""Fire Wall prepared creation,typed fields and original saved maker progression."""
import os,sys
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_blaze import configuration as blaze

def configuration(full=False):
 c=blaze(full);c['stage']=ROOT/'.local/class-skills/sorceress-fire-wall'
 c['extra_families']+=('fire-wall',);c['extra_target_input_commands']+=('fire-wall-right','fire-wall-quick')
 c['extra_connected_checks']+=('fire-wall-reload-check',);c['extra_wait_results']['fire-wall-reload-check']='fire-wall-reload-result'
 for name in ('creation','damage','return'):c['extra_files']['fire-wall-'+name+'-source-cases.json']=ROOT/('.local/class-skills/fire-wall-'+name+'-source/cases.json')
 c['extra_inputs']+=(Path(__file__).resolve(),ROOT/'tools/reference/fire-wall-creation-source-evidence.json',ROOT/'tools/reference/fire-wall-damage-source-evidence.json',ROOT/'tools/reference/fire-wall-return-source-evidence.json')
 return c
if __name__=='__main__':
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**configuration('--full' in sys.argv))
