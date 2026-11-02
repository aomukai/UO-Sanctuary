#!/usr/bin/env python3
"""Learned Lightning Sentry with retained CBS input, bolt and retirement checks."""
import os
from pathlib import Path
from check_sentry_learned import ROOT,configuration as previous
from check_iron_golem_birth import check

def configuration():
    config=previous();config['stage']=ROOT/'.local/class-skills/lightning-learned'
    config['extra_files']['lightning-sentry-damage-source-probe.json']=ROOT/'.local/class-skills/lightning-sentry-damage-source-probe.json'
    config['extra_families']=(*config['extra_families'],'lightning-sentry','lightning-learned')
    config['extra_checks']=(*config['extra_checks'],'lightning-learned-mechanics-check','lightning-sentry-live-check')
    config['extra_connected_checks']=(*config['extra_connected_checks'],'lightning-learned-check','lightning-learned-reload-check','lightning-sentry-live-check')
    config['extra_wait_results'].update({'lightning-learned-check':'lightning-learned-result','lightning-learned-reload-check':'lightning-learned-reload-result','lightning-sentry-live-check':'lightning-sentry-live-result'})
    inputs=('lightning-learned-left','lightning-learned-right','lightning-learned-quick')
    config['extra_target_input_commands']=(*config['extra_target_input_commands'],*inputs)
    config['extra_ground_input_commands']=config['extra_target_input_commands']
    config['extra_instant_input_commands']=(*config['extra_instant_input_commands'],'lightning-learned-left')
    config['extra_inputs']=(*config['extra_inputs'],Path(__file__).resolve(),ROOT/'tools/reference/sentry-learned-native-evidence.json',ROOT/'tools/reference/d2r-local-sentry-model-evidence.json',ROOT/'tools/reference/d2r-local-sentry-animation-evidence.json')
    return config

if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**configuration())
