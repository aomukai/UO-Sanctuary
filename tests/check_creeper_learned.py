#!/usr/bin/env python3
"""Learned Creeper public casting, approved snapshot and retained class checks."""
import argparse
import os
from pathlib import Path
from check_recycler_learned import ROOT, configuration as previous
from check_iron_golem_birth import check


def configuration(broader=False):
    c=previous(broader)
    c['stage']=ROOT/'.local/class-skills'/('creeper-learned-broader' if broader else 'creeper-learned')
    c['extra_families']=('creeper-learned','creeper-poison','creeper-ground','creeper-world','creeper-body','creeper-balance')+c['extra_families']
    c['extra_checks']=('creeper-ai-check','creeper-stats-check','creeper-fields-check')+c['extra_checks']
    c['extra_live_commands']=('creeper-learned-live',)+c['extra_live_commands']
    inputs=('creeper-learned-right','creeper-learned-quick')
    c['extra_target_input_commands']+=inputs
    c['extra_ground_input_commands']+=inputs
    c['extra_inputs']+=(Path(__file__).resolve(),ROOT/'tools/reference/summon-snapshot-decision.json')
    for name in ('creeper-ai','creeper-components','creeper-vines-age','creeper-frame'):
        c['extra_files'][name+'-source-probe.json']=ROOT/'.local/class-skills'/(name+'-source-probe.json')
    return c


if __name__=='__main__':
    parser=argparse.ArgumentParser()
    parser.add_argument('--broader',action='store_true')
    args=parser.parse_args()
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(**configuration(args.broader))
