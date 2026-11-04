#!/usr/bin/env python3
"""Approved learned Lightning Strike with retained melee and player/Mage chain scopes."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_charged_strike_velocity import configuration
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    c=configuration();c['stage']=ROOT/'.local/class-skills/lightning-strike'
    c['extra_families']+=('lightning-strike','player-chain','mage-chain')
    c['extra_checks']+=('chain-child-search-check',)
    c['extra_files'].update({'lightning-strike-damage-source.json':ROOT/'.local/class-skills/lightning-strike-damage-source/cases.json',
        'chain-child-filter-source.json':ROOT/'.local/class-skills/chain-child-filter-source/cases.json'})
    c['extra_wait_results']['player-chain-reload-check']='player-chain-reload-result'
    c['extra_connected_checks']=('player-chain-reload-check',)
    c['extra_target_input_commands']=('lightning-strike-right','lightning-strike-quick','player-chain-right','player-chain-quick')
    c['extra_live_commands']=('mage-chain-live','player-chain-offline')
    c['extra_inputs']+=(Path(__file__).resolve(),ROOT/'tools/reference/lightning-strike-sanctuary-decision.json')
    check(**c)
