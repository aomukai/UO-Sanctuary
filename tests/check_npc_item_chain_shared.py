#!/usr/bin/env python3
"""Retain player/Mage chain motion and the approved Lightning Strike opening."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_npc_item_spell_flights_shared import configuration as prior
def configuration():
 c=prior(False);c['stage']=ROOT/'.local/class-skills/npc-item-chain-shared';c['extra_families']+=('lightning-strike',)
 c['extra_checks']+=('chain-child-search-check',)
 c['extra_files'].update({'lightning-strike-damage-source.json':ROOT/'.local/class-skills/lightning-strike-damage-source/cases.json',
                          'chain-child-filter-source.json':ROOT/'.local/class-skills/chain-child-filter-source/cases.json'})
 c['extra_target_input_commands']+=('lightning-strike-right','lightning-strike-quick')
 c['extra_inputs']+=(Path(__file__).resolve(),ROOT/'tools/reference/lightning-strike-sanctuary-decision.json');return c
if __name__=='__main__':
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**configuration())
