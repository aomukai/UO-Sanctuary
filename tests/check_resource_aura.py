#!/usr/bin/env python3
"""Resource aura direct callbacks,rank/payment composition and delayed actual inputs."""
import os,sys
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_blessed_aim_aura import configuration as parent
def configuration(full=False):
    c=parent(full);c['stage']=ROOT/'.local/class-skills/resource-aura'
    c['extra_checks']+=('resource-aura-check',);c['extra_wait_results']['resource-aura-check']='resource-aura-result'
    names=tuple('resource-aura-'+ability+'-'+mode for ability in ('prayer','cleansing','meditation') for mode in ('right','quick'))
    c['extra_target_input_commands']+=names;c['extra_instant_input_commands']+=names
    c['extra_inputs']+=(Path(__file__).resolve(),ROOT/'tools/probe_resource_aura_callback.py',ROOT/'tools/reference/resource-aura-callback-source-evidence.json',ROOT/'.local/class-skills/resource-aura-callback-source/cases.json')
    return c
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**configuration('--full' in sys.argv))
