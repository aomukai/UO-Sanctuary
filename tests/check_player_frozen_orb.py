#!/usr/bin/env python3
"""Prepared Frozen Orb with retained Sorceress/Meteor/Blizzard and shared collision scopes."""
import os,sys
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_player_blizzard import configuration as blizzard

def configuration(full=False):
 c=blizzard(full);c['stage']=ROOT/'.local/class-skills/sorceress-player-frozen-orb'
 c['extra_families']+=('player-frozen-orb',);c['extra_target_input_commands']+=('player-frozen-orb-left','player-frozen-orb-right','player-frozen-orb-quick')
 c['extra_connected_checks']+=('frozen-orb-check','player-frozen-orb-reload-check',);c['extra_wait_results']['player-frozen-orb-reload-check']='player-frozen-orb-reload-result'
 for name in ('creation','damage','emission','collision'):c['extra_files']['frozen-orb-'+name+'-source-cases.json']=ROOT/('.local/class-skills/frozen-orb-'+name+'-source/cases.json')
 c['extra_inputs']+=(Path(__file__).resolve(),*(ROOT/('tools/reference/frozen-orb-'+name+'-source-evidence.json') for name in ('creation','damage','emission','collision')))
 return c
if __name__=='__main__':
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**configuration('--full' in sys.argv))
