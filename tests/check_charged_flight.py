#!/usr/bin/env python3
"""Saved Charged Bolt volleys with retained Sorceress scopes."""
import os
from pathlib import Path
import sys
from check_iron_golem_birth import ROOT,check
from check_lightning_flight import configuration as lightning

def configuration(full=False):
    config=lightning(full);config['stage']=ROOT/'.local/class-skills/sorceress-charged-flight'
    config['extra_families']+=('charged-flight',)
    config['extra_wait_results']['charged-flight-reload-check']='charged-flight-reload-result'
    config['extra_connected_checks']+=('charged-flight-reload-check',)
    config['extra_target_input_commands']+=('charged-flight-right','charged-flight-quick')
    config['extra_files']['sorceress-passive-rules.json']=ROOT/'tools/reference/sorceress-passive-rules.json'
    config['extra_files']['player-charged-bolt-emitter-cases.json']=ROOT/'.local/class-skills/player-charged-bolt-emitter-source/cases.json'
    config['extra_inputs']+=(Path(__file__).resolve(),)
    return config
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(**configuration('--full' in sys.argv))
