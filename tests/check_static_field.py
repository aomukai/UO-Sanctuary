#!/usr/bin/env python3
"""Player Static Field input/saves plus retained Mage and Sorceress regression scopes."""
import os
from pathlib import Path
import sys
from check_iron_golem_birth import ROOT,check
from check_sorceress_nova import configuration as nova

def configuration(full=False):
    config=nova(full);config['stage']=ROOT/'.local/class-skills/sorceress-static-field'
    config['extra_families']+=('static-field','mage-static')
    config['extra_live_commands']=(*config.get('extra_live_commands',()),'mage-static-live')
    config['extra_wait_results']['static-field-reload-check']='static-field-reload-result'
    config['extra_connected_checks']+=('static-field-reload-check',)
    config['extra_target_input_commands']+=('static-field-right','static-field-quick')
    config['extra_files']['static-field-source-cases.json']=ROOT/'.local/class-skills/static-field-source/cases.json'
    config['extra_inputs']+=(Path(__file__).resolve(),)
    return config
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(**configuration('--full' in sys.argv))
