#!/usr/bin/env python3
"""Approved Teleport,exact resource conservation and actual world-cursor input."""
import os,sys
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_hydra_world import configuration as hydra
def configuration(full=False):
 c=hydra(full);c['stage']=ROOT/'.local/class-skills/player-teleport'
 c['extra_families']+=('player-teleport',)
 c['extra_connected_checks']+=('player-teleport-reload-check',)
 c['extra_wait_results']['player-teleport-reload-check']='player-teleport-reload-result'
 names=('player-teleport-left','player-teleport-right','player-teleport-quick')
 c['extra_target_input_commands']+=names
 c['extra_ground_input_commands']=c.get('extra_ground_input_commands',())+names
 c['extra_inputs']+=(Path(__file__).resolve(),)
 c['save_command']='player-teleport-save';c['save_result']='player-teleport-save'
 return c
if __name__=='__main__':
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**configuration('--full' in sys.argv))
