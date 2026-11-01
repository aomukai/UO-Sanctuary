#!/usr/bin/env python3
"""Player Glacial Spike and retained player/Mage cold spell regressions."""
import os
from pathlib import Path
import sys
from check_iron_golem_birth import ROOT,check
from check_ice_blast import configuration as cold


def configuration(full=False):
    config=cold(full)
    config['stage']=ROOT/'.local/class-skills/sorceress-glacial-spike'
    config['extra_families']+=('glacial-spike','mage-glacial')
    config['extra_live_commands']=(*config.get('extra_live_commands',()),'mage-glacial-live','glacial-player-live')
    config['extra_wait_results']['glacial-spike-reload-check']='glacial-spike-reload-result'
    config['extra_connected_checks']+=('glacial-spike-reload-check',)
    config['extra_target_input_commands']+=('glacial-spike-right','glacial-spike-quick')
    config['extra_inputs']+=(Path(__file__).resolve(),)
    return config


if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(**configuration('--full' in sys.argv))
