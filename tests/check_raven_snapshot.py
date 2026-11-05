#!/usr/bin/env python3
"""Raven birth policy, retained source/lifecycle/input and class regressions."""
import argparse
import os
from pathlib import Path
from check_druid_snapshot import ROOT,configuration as previous
from check_iron_golem_birth import check

FAMILIES=('raven-snapshot','raven-body','raven-casting')
def configuration(broader=False,compact=False):
    c=previous(broader) if not compact else dict(extra_families=(),extra_checks=(),extra_live_commands=(),extra_target_input_commands=(),extra_inputs=(),extra_files={})
    c['stage']=ROOT/'.local/class-skills'/('raven-snapshot-compact' if compact else 'raven-snapshot-broader' if broader else 'raven-snapshot')
    c['extra_families']=FAMILIES+c['extra_families']
    c['extra_checks']=('raven-stats-check','raven-flight-check')+c['extra_checks']
    c['extra_live_commands']=('raven-snapshot-live',)+c['extra_live_commands']
    c['extra_target_input_commands']+=('raven-right','raven-quick')
    c['extra_ground_input_commands']=tuple(c.get('extra_ground_input_commands',()))+('raven-right','raven-quick')
    c['extra_inputs']+=(Path(__file__).resolve(),)
    for name in ('raven-components','raven-circle'):
        c['extra_files'][name+'-source-probe.json']=ROOT/'.local/class-skills'/(name+'-source-probe.json')
    return c
if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--broader',action='store_true');parser.add_argument('--compact',action='store_true');args=parser.parse_args()
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(**configuration(args.broader,args.compact))
