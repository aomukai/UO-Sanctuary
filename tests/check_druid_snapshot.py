#!/usr/bin/env python3
"""Current Druid snapshot casts, retained upgrades, original save regressions."""
import argparse
import os
from pathlib import Path
from check_necro_snapshot import ROOT,configuration as previous
from check_blood_life_tap import configuration as old
from check_iron_golem_birth import check

FAMILIES=('druid-snapshot','spirit-wolf-upgrades','dire-wolf-upgrades','grizzly-upgrades','dire-wolf-world','grizzly-world','spirit-wolf-mode')
LIVE=('spirit-wolf-upgrades-live-check','dire-wolf-upgrades-live-check','grizzly-upgrades-live-check')
def configuration(broader=False):
    c=previous(broader)
    c['stage']=ROOT/'.local/class-skills'/('druid-snapshot-broader' if broader else 'druid-snapshot')
    c['extra_families']=FAMILIES+c['extra_families']
    c['extra_checks']=LIVE+c['extra_checks']
    c['extra_connected_checks']=LIVE+tuple(c.get('extra_connected_checks',()))
    c['extra_wait_results']={**c.get('extra_wait_results',{}),**{s:s.replace('-check','-result')for s in LIVE}}
    c['extra_inputs']+=(Path(__file__).resolve(),)
    for script in ('check_spirit_wolf_upgrades.py','check_dire_wolf_upgrades.py','check_grizzly_upgrades.py'):
        c['extra_files'].update(old(script)['extra_files'])
    return c
if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--broader',action='store_true');args=parser.parse_args()
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(**configuration(args.broader))
