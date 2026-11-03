#!/usr/bin/env python3
"""Learned Death Sentry hybrid combat with all three prior sentry cohorts."""
import os
from pathlib import Path
from check_inferno_learned import ROOT,configuration as previous
from check_iron_golem_birth import check

def configuration():
    config=previous();config['stage']=ROOT/'.local/class-skills/death-learned'
    for name in ('death-sentry-ai','death-sentry-corpse-search','corpse-explosion','death-sentry-clock','corpse-base-life'):
        config['extra_files'][name+'-source-probe.json']=ROOT/'.local/class-skills'/(name+'-source-probe.json')
    config['extra_families']=(*config['extra_families'],'corpse-base','death-explosion','death-control','death-learned')
    config['extra_manifest_names']['death-explosion']='death-sentry-explosion-fixtures.json'
    config['extra_checks']=(*config['extra_checks'],'death-learned-mechanics-check')
    config['extra_connected_checks']=(*config['extra_connected_checks'],'death-learned-check','death-learned-reload-check')
    config['extra_wait_results'].update({'corpse-base-check':'corpse-base-result','corpse-base-reload-check':'corpse-base-result','death-learned-check':'death-learned-result','death-learned-reload-check':'death-learned-reload-result'})
    inputs=('death-learned-left','death-learned-right','death-learned-quick')
    config['extra_target_input_commands']=(*config['extra_target_input_commands'],*inputs)
    config['extra_ground_input_commands']=config['extra_target_input_commands']
    config['extra_instant_input_commands']=(*config['extra_instant_input_commands'],'death-learned-left')
    config['extra_inputs']=(*config['extra_inputs'],Path(__file__).resolve(),ROOT/'tools/reference/inferno-learned-native-evidence.json')
    return config

if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**configuration())
