#!/usr/bin/env python3
"""Thunder Storm live pulse and lifecycle with retained Sorceress scopes."""
import os
from pathlib import Path
import sys
from check_iron_golem_birth import ROOT,check
from check_player_chain import configuration as chain

def configuration(full=False):
    config=chain(full);config['stage']=ROOT/'.local/class-skills/sorceress-storm-pulse'
    config['extra_families']+=('storm-pulse',)
    config['extra_wait_results']['storm-pulse-reload-check']='storm-pulse-reload-result'
    config['extra_connected_checks']+=('storm-pulse-reload-check','thunderstorm-check','thunderstorm-reload-check')
    config['extra_target_input_commands']+=('storm-pulse-right','storm-pulse-quick')
    config['extra_files']['sorceress-passive-rules.json']=ROOT/'tools/reference/sorceress-passive-rules.json'
    config['extra_files']['player-charged-bolt-emitter-cases.json']=ROOT/'.local/class-skills/player-charged-bolt-emitter-source/cases.json'
    config['extra_live_commands']+=('storm-pulse-offline',)
    config['extra_files']['thunder-storm-source-cases.json']=ROOT/'.local/class-skills/thunder-storm-source/cases.json'
    config['extra_inputs']+=(Path(__file__).resolve(),)
    return config
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(**configuration('--full' in sys.argv))
