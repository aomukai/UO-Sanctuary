#!/usr/bin/env python3
"""Saved Nova/Frost Nova with retained Sorceress and Poison Nova regressions."""
import os
from pathlib import Path
import sys
from check_iron_golem_birth import ROOT,check
from check_fire_ball import configuration as fire

def configuration(full=False):
    config=fire(full);config['stage']=ROOT/'.local/class-skills/sorceress-nova'
    config['extra_families']+=('nova','frost-nova','poison-nova')
    for key in ('nova','frost-nova'):
        config['extra_wait_results'][key+'-reload-check']=key+'-reload-result'
        config['extra_connected_checks']+=(key+'-reload-check',)
        config['extra_target_input_commands']+=(key+'-right',key+'-quick')
    config['extra_files'].update({
        'sorceress-nova-emitter-source-cases.json':ROOT/'.local/class-skills/sorceress-nova-emitter/cases.json',
        'missile-acceleration-source-probe.json':ROOT/'.local/class-skills/missile-acceleration-source-probe.json',
        'poison-nova-damage-source-cases.json':ROOT/'.local/class-skills/poison-nova-damage/cases.json',
        'poison-nova-emitter-source-cases.json':ROOT/'.local/class-skills/poison-nova-emitter/cases.json'})
    config['extra_inputs']+=(Path(__file__).resolve(),)
    return config
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(**configuration('--full' in sys.argv))
