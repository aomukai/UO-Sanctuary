#!/usr/bin/env python3
"""Automatic Wake firing/retirement with the retained maker and sentry cohorts."""
import argparse,os
from pathlib import Path
from check_wake_fire_missiles import ROOT,configuration as previous
from check_iron_golem_birth import check

def configuration():
    c=previous();c['stage']=ROOT/'.local/class-skills/wake-fire-automatic'
    c['extra_families']=(*c['extra_families'],'wake-fire-automatic')
    c['extra_checks']=(*c['extra_checks'],'wake-fire-automatic-live-check')
    c['extra_connected_checks']=(*c['extra_connected_checks'],'wake-fire-automatic-live-check')
    c['extra_wait_results']['wake-fire-automatic-live-check']='wake-fire-automatic-live-result'
    c['extra_inputs']=(*c['extra_inputs'],Path(__file__).resolve(),ROOT/'tools/reference/wake-fire-missiles-native-evidence.json')
    return c
if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--focused',action='store_true');args=parser.parse_args();c=configuration()
    if args.focused:
        c['stage']=ROOT/'.local/class-skills/wake-fire-automatic-targeted'
        c['extra_families']=('wake-fire-prepared','wake-fire-actor','wake-fire-missile','wake-fire-automatic')
        c['extra_checks']=('wake-fire-automatic-live-check',)
        c['extra_connected_checks']=('wake-fire-automatic-live-check',)
        c['extra_wait_results']={'wake-fire-automatic-live-check':'wake-fire-automatic-live-result'}
        for key in ('extra_target_input_commands','extra_ground_input_commands','extra_instant_input_commands'):c[key]=()
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**c)
