#!/usr/bin/env python3
"""Real prepared Wake maker/waves while retaining learned four-sentry acceptance."""
import os
from pathlib import Path
from check_death_learned import ROOT,configuration as previous
from check_iron_golem_birth import check

def configuration():
    config=previous();config['stage']=ROOT/'.local/class-skills/wake-fire-missiles'
    for name in ('wake-of-fire-clock','wake-of-fire-damage','wake-of-fire-factory'):
        config['extra_files'][name+'-source-probe.json']=ROOT/'.local/class-skills'/(name+'-source-probe.json')
    config['extra_families']=(*config['extra_families'],'wake-fire-prepared','wake-fire-actor','wake-fire-missile')
    config['extra_inputs']=(*config['extra_inputs'],Path(__file__).resolve(),ROOT/'tools/reference/death-learned-native-evidence.json')
    return config

if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**configuration())
