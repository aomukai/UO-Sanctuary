#!/usr/bin/env python3
"""Hydra native emitter, clock, typed flight, actual input and original saves."""
import os,sys
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_hydra_prepared import configuration as prepared
def configuration(full=False):
 c=prepared(full);c['stage']=ROOT/'.local/class-skills/hydra-world'
 c['extra_families']+=('hydra-world',)
 c['extra_connected_checks']+=('hydra-world-reload-check',)
 c['extra_wait_results']['hydra-world-reload-check']='hydra-world-reload-result'
 c['extra_target_input_commands']+=('hydra-world-right','hydra-world-quick')
 c['extra_inputs']+=(Path(__file__).resolve(),)
 return c
if __name__=='__main__':
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**configuration('--full' in sys.argv))
