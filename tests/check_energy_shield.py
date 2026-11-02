#!/usr/bin/env python3
"""Energy Shield cast/absorption/input/save checks with retained class contracts."""
import os
from pathlib import Path
import sys
from check_iron_golem_birth import ROOT, check
from check_shiver_armor import configuration as shiver

def configuration(full=False):
    config=shiver(full);config['stage']=ROOT/'.local/class-skills/sorceress-energy-shield'
    config['extra_families']+=('energy-shield',)
    config['extra_target_input_commands']+=('energy-shield-right','energy-shield-quick')
    config['extra_files']['energy-shield-source-cases.json']=ROOT/'.local/class-skills/energy-shield-source-preparation/cases.json'
    config['extra_files']['energy-shield-cast-source-cases.json']=ROOT/'.local/class-skills/energy-shield-cast-source/cases.json'
    config['extra_inputs']+=(Path(__file__).resolve(),ROOT/'tools/reference/energy-shield-source-evidence.json',ROOT/'tools/reference/energy-shield-cast-source-evidence.json')
    return config

if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(**configuration('--full' in sys.argv))
