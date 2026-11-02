#!/usr/bin/env python3
"""Learned right/quick CBS casting and retained actor/bolt/death cohorts."""
import os
from pathlib import Path
from check_sentry_retirement import ROOT,configuration as previous
from check_iron_golem_birth import check

def configuration():
    config=previous();config['stage']=ROOT/'.local/class-skills/sentry-learned'
    config['extra_families']=(*config['extra_families'],'sentry-learned')
    config['extra_checks']=(*config['extra_checks'],'sentry-learned-availability-check','sentry-learned-mechanics-check')
    config['extra_connected_checks']=(*config['extra_connected_checks'],'sentry-learned-check','sentry-learned-reload-check')
    config['extra_wait_results'].update({'sentry-learned-check':'sentry-learned-result','sentry-learned-reload-check':'sentry-learned-reload-result'})
    config['extra_target_input_commands']=('sentry-learned-left','sentry-learned-right','sentry-learned-quick')
    config['extra_ground_input_commands']=config['extra_target_input_commands']
    config['extra_instant_input_commands']=('sentry-learned-left',)
    config['extra_inputs']=(*config['extra_inputs'],Path(__file__).resolve(),ROOT/'tools/reference/sentry-retirement-native-evidence.json')
    return config

if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**configuration())
