#!/usr/bin/env python3
"""Prepared player Chain Lightning with retained player/Mage scopes."""
import os
from pathlib import Path
import sys
from check_iron_golem_birth import ROOT,check
from check_charged_flight import configuration as charged

def configuration(full=False):
    config=charged(full);config['stage']=ROOT/'.local/class-skills/sorceress-player-chain'
    config['extra_families']+=('player-chain','mage-chain')
    config['extra_wait_results']['player-chain-reload-check']='player-chain-reload-result'
    config['extra_connected_checks']+=('player-chain-reload-check',)
    config['extra_target_input_commands']+=('player-chain-right','player-chain-quick')
    config['extra_files']['sorceress-passive-rules.json']=ROOT/'tools/reference/sorceress-passive-rules.json'
    config['extra_files']['player-charged-bolt-emitter-cases.json']=ROOT/'.local/class-skills/player-charged-bolt-emitter-source/cases.json'
    config['extra_live_commands']+=('mage-chain-live','player-chain-offline')
    config['extra_inputs']+=(Path(__file__).resolve(),)
    return config
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(**configuration('--full' in sys.argv))
