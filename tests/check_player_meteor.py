#!/usr/bin/env python3
"""Prepared player Meteor using retained ground fields and original saved warnings."""
import os,sys
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_fire_wall import configuration as wall

def configuration(full=False):
 c=wall(full);c['stage']=ROOT/'.local/class-skills/sorceress-player-meteor'
 c['extra_families']+=('mage-meteor','player-meteor');c['extra_target_input_commands']+=('player-meteor-right','player-meteor-quick')
 c['extra_files']['mage-meteor-source-probe.json']=ROOT/'.local/class-skills/mage-meteor-source-probe.json'
 c['extra_live_commands']+=('mage-meteor-live',)
 c['extra_connected_checks']+=('player-meteor-reload-check',);c['extra_wait_results']['player-meteor-reload-check']='player-meteor-reload-result'
 for name in ('creation','damage'):c['extra_files']['meteor-'+name+'-source-cases.json']=ROOT/('.local/class-skills/meteor-'+name+'-source/cases.json')
 c['extra_inputs']+=(Path(__file__).resolve(),ROOT/'tools/reference/meteor-creation-source-evidence.json',ROOT/'tools/reference/meteor-damage-source-evidence.json',ROOT/'tools/reference/mage-meteor-source-evidence.json')
 return c
if __name__=='__main__':
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**configuration('--full' in sys.argv))
