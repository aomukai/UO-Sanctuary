#!/usr/bin/env python3
"""Saved player Fire Ball with retained player/Mage projectile and area scopes."""
import os
from pathlib import Path
import sys
from check_iron_golem_birth import ROOT,check
from check_glacial_spike import configuration as glacial


def configuration(full=False):
    config=glacial(full)
    config['stage']=ROOT/'.local/class-skills/sorceress-fire-ball'
    config['extra_families']+=('fire-ball',)
    config['extra_wait_results']['fire-ball-reload-check']='fire-ball-reload-result'
    config['extra_connected_checks']+=('fire-ball-reload-check',)
    config['extra_target_input_commands']+=('fire-ball-right','fire-ball-quick')
    config['extra_files']['exploding-arrow-callback-source-probe.json']=ROOT/'.local/class-skills/exploding-arrow-callback-source-probe.json'
    config['extra_inputs']+=(Path(__file__).resolve(),)
    return config


if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(**configuration('--full' in sys.argv))
