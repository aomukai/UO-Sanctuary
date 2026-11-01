#!/usr/bin/env python3
"""Four passive contracts and modern broken-immunity correction with retained Sorceress checks."""
import os
from pathlib import Path
import sys
from check_iron_golem_birth import ROOT,check
from check_static_field import configuration as static

def configuration(full=False):
    config=static(full);config['stage']=ROOT/'.local/class-skills/sorceress-passives'
    config['extra_families']+=('sorceress-passives',)
    config['extra_wait_results']['sorceress-passives-reload-check']='sorceress-passives-reload-result'
    config['extra_connected_checks']+=('sorceress-passives-reload-check',)
    config['extra_files']['sorceress-passive-rules.json']=ROOT/'tools/reference/sorceress-passive-rules.json'
    config['extra_inputs']+=(Path(__file__).resolve(),)
    return config
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(**configuration('--full' in sys.argv))
