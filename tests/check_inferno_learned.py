#!/usr/bin/env python3
"""Learned Wake of Inferno with distinct E9/channel and retained Sentry cohorts."""
import os
from pathlib import Path
from check_lightning_learned import ROOT,configuration as previous
from check_iron_golem_birth import check

def configuration():
    config=previous();config['stage']=ROOT/'.local/class-skills/inferno-learned'
    for name in ('inferno-sentry-controller-source-probe.json','wake-of-inferno-damage-source-probe.json'):
        config['extra_files'][name]=ROOT/'.local/class-skills'/name
    config['extra_families']=(*config['extra_families'],'inferno-sentry','inferno-learned')
    config['extra_checks']=(*config['extra_checks'],'inferno-learned-mechanics-check','inferno-sentry-live-check')
    config['extra_connected_checks']=(*config['extra_connected_checks'],'inferno-learned-check','inferno-learned-reload-check','inferno-sentry-live-check')
    config['extra_wait_results'].update({'inferno-learned-check':'inferno-learned-result','inferno-learned-reload-check':'inferno-learned-reload-result','inferno-sentry-live-check':'inferno-sentry-live-result'})
    inputs=('inferno-learned-left','inferno-learned-right','inferno-learned-quick')
    config['extra_target_input_commands']=(*config['extra_target_input_commands'],*inputs)
    config['extra_ground_input_commands']=config['extra_target_input_commands']
    config['extra_instant_input_commands']=(*config['extra_instant_input_commands'],'inferno-learned-left')
    config['extra_inputs']=(*config['extra_inputs'],Path(__file__).resolve(),ROOT/'tools/reference/lightning-learned-native-evidence.json')
    return config

if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**configuration())
