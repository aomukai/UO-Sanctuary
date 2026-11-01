#!/usr/bin/env python3
"""Saved Lightning passing flights with retained Sorceress scopes."""
import os
from pathlib import Path
import sys
from check_iron_golem_birth import ROOT,check
from check_sorceress_passives import configuration as passives

def configuration(full=False):
    config=passives(full);config['stage']=ROOT/'.local/class-skills/sorceress-lightning-flight'
    config['extra_families']+=('lightning-flight',)
    config['extra_wait_results']['lightning-flight-reload-check']='lightning-flight-reload-result'
    config['extra_connected_checks']+=('lightning-flight-reload-check',)
    config['extra_target_input_commands']+=('lightning-flight-right','lightning-flight-quick')
    config['extra_files']['sorceress-passive-rules.json']=ROOT/'tools/reference/sorceress-passive-rules.json'
    config['extra_inputs']+=(Path(__file__).resolve(),)
    return config
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(**configuration('--full' in sys.argv))
