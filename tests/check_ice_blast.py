#!/usr/bin/env python3
"""Player Ice Blast freeze/contact/input/reload and shared freeze regressions."""
import os
from pathlib import Path
import sys
from check_iron_golem_birth import ROOT, check
from check_ice_bolt import configuration as ice


def configuration(full=False):
    config=ice(full)
    config['stage']=ROOT/'.local/class-skills/sorceress-ice-blast'
    config['extra_families']+=('ice-blast',)
    config['extra_checks']=(*config.get('extra_checks',()),'freeze-state-check')
    config['extra_wait_results']['ice-blast-reload-check']='ice-blast-reload-result'
    config['extra_connected_checks']+=('ice-blast-reload-check',)
    config['extra_target_input_commands']+=('ice-blast-right','ice-blast-quick')
    config['extra_files']['freeze-state-source-probe.json']=ROOT/'.local/class-skills/freeze-state-source-probe.json'
    config['extra_inputs']+=(Path(__file__).resolve(),)
    return config


if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(**configuration('--full' in sys.argv))
