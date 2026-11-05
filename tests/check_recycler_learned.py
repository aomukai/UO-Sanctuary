#!/usr/bin/env python3
"""Learned recycler input/world/recovery and retained current class regressions."""
import argparse,os
from pathlib import Path
from check_npc_kill_procs import ROOT,configuration as previous
from check_iron_golem_birth import check

def configuration(broader=False):
 c=previous(broader);c['stage']=ROOT/'.local/class-skills'/('recycler-learned-broader' if broader else 'recycler-learned')
 c['extra_families']=('recycler-learned','vine-cast-windup','creeper-casting','recycler-casting','recycler-world','recycler-recovery','recycler-body')+c['extra_families']
 c['extra_checks']=('recycler-model-check','vine-cast-windup-live-check')+c.get('extra_checks',())
 c['extra_connected_checks']=('vine-cast-windup-live-check',)+c.get('extra_connected_checks',())
 c['extra_wait_results']={**c.get('extra_wait_results',{}),'vine-cast-windup-live-check':'vine-cast-windup-live-result'}
 c['extra_live_commands']=('recycler-learned-live',)+c.get('extra_live_commands',())
 inputs=('carrion-right','carrion-quick','solar-right','solar-quick');c['extra_target_input_commands']+=inputs;c['extra_ground_input_commands']+=inputs
 c['extra_inputs']+=(Path(__file__).resolve(),ROOT/'tools/reference/vine-sanctuary-decisions.json')
 for name in ('recycler-vines-birth','recycler-vines-ai','recycler-vines','pet-walk-flags'):c['extra_files'][name+'-source-probe.json']=ROOT/'.local/class-skills'/(name+'-source-probe.json')
 return c
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--broader',action='store_true');a=p.parse_args();os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**configuration(a.broader))
