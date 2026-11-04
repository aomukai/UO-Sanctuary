#!/usr/bin/env python3
"""Learned Wake of Fire with all retained sentry and wave/retirement cohorts."""
import argparse,os
from pathlib import Path
from check_wake_fire_automatic import ROOT,configuration as previous
from check_iron_golem_birth import check

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--focused',action='store_true');args=parser.parse_args()
    c=previous();c['stage']=ROOT/'.local/class-skills/wake-learned'
    c['extra_families']+=('wake-learned',)
    c['extra_checks']+=('wake-learned-mechanics-check',)
    c['extra_connected_checks']+=('wake-learned-check','wake-learned-reload-check')
    c['extra_wait_results'].update({'wake-learned-check':'wake-learned-result','wake-learned-reload-check':'wake-learned-reload-result'})
    c['extra_target_input_commands']+=('wake-learned-left','wake-learned-right','wake-learned-quick')
    c['extra_ground_input_commands']=c['extra_target_input_commands']
    c['extra_instant_input_commands']+=('wake-learned-left',)
    c['extra_inputs']+=(Path(__file__).resolve(),ROOT/'tools/reference/wake-learned-sanctuary-decision.json')
    if args.focused:
        c['stage']=ROOT/'.local/class-skills/wake-learned-targeted'
        c['extra_families']=('wake-fire-prepared','wake-fire-actor','wake-fire-missile','wake-fire-automatic','wake-learned')
        c['extra_checks']=('wake-fire-automatic-live-check','wake-learned-mechanics-check')
        c['extra_connected_checks']=('wake-fire-automatic-live-check','wake-learned-check','wake-learned-reload-check')
        c['extra_wait_results']={'wake-fire-automatic-live-check':'wake-fire-automatic-live-result','wake-learned-check':'wake-learned-result','wake-learned-reload-check':'wake-learned-reload-result'}
        c['extra_target_input_commands']=('wake-learned-left','wake-learned-right','wake-learned-quick')
        c['extra_ground_input_commands']=c['extra_target_input_commands']
        c['extra_instant_input_commands']=('wake-learned-left',)
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**c)
