#!/usr/bin/env python3
"""Blaze cast/emission/typed field persistence with retained class contracts."""
import os,sys
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_buff_payment import configuration as buffs

def configuration(full=False):
    c=buffs(full);c['stage']=ROOT/'.local/class-skills/sorceress-blaze'
    c['extra_families']+=('blaze',)
    c['extra_target_input_commands']+=('blaze-right','blaze-quick')
    c['extra_connected_checks']+=('blaze-reload-check',)
    c['extra_wait_results']['blaze-reload-check']='blaze-reload-result'
    c['extra_files']['blaze-damage-source-cases.json']=ROOT/'.local/class-skills/blaze-damage-source/cases.json'
    c['extra_inputs']+=(Path(__file__).resolve(),ROOT/'tools/reference/blaze-emitter-source-evidence.json',ROOT/'tools/reference/blaze-damage-source-evidence.json')
    return c
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**configuration('--full' in sys.argv))
