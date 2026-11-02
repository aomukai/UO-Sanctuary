#!/usr/bin/env python3
"""Prepared player Blizzard with retained class/Meteor/ground-field regression."""
import os,sys
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_player_meteor import configuration as meteor

def configuration(full=False):
 c=meteor(full);c['stage']=ROOT/'.local/class-skills/sorceress-player-blizzard'
 c['extra_families']+=('player-blizzard',);c['extra_target_input_commands']+=('player-blizzard-right','player-blizzard-quick')
 c['extra_connected_checks']+=('player-blizzard-reload-check',);c['extra_wait_results']['player-blizzard-reload-check']='player-blizzard-reload-result'
 for name in ('creation','center','damage','collision'):c['extra_files']['blizzard-'+name+'-source-cases.json']=ROOT/('.local/class-skills/blizzard-'+name+'-source/cases.json')
 c['extra_inputs']+=(Path(__file__).resolve(),*(ROOT/('tools/reference/blizzard-'+name+'-source-evidence.json') for name in ('creation','center','damage','collision')))
 return c
if __name__=='__main__':
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**configuration('--full' in sys.argv))
