#!/usr/bin/env python3
"""Natural CBS exhaustion, cap replacement and saved original dead sources."""
import os
from pathlib import Path
from check_limited_corpses import ROOT,configuration as previous
from check_iron_golem_birth import check

def configuration():
    config=previous();config['stage']=ROOT/'.local/class-skills/sentry-retirement'
    config['extra_files']['sentry-death-clock-source-probe.json']=ROOT/'.local/class-skills/sentry-death-clock-source-probe.json'
    config['extra_families']=(*config['extra_families'][:-1],'sentry-retirement')
    config['extra_connected_checks']=('sentry-retirement-check','sentry-retirement-reload-check')
    config['extra_wait_results']={'sentry-retirement-check':'sentry-retirement-result','sentry-retirement-reload-check':'sentry-retirement-reload-result'}
    config['extra_inputs']=(*config['extra_inputs'],Path(__file__).resolve(),ROOT/'tools/reference/sentry-death-clock-source-evidence.json')
    return config

if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**configuration())
