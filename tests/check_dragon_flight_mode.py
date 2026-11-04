#!/usr/bin/env python3
"""Fixed Flight clock with accepted shared SQ21 and learned Tail cohorts."""
import argparse
import os
from pathlib import Path
from check_dragon_flight_preparation import ROOT,configuration as previous
from check_iron_golem_birth import check


def configuration(broader=False):
    config=previous(broader)
    config['stage']=ROOT/'.local/class-skills'/('dragon-flight-mode-broader' if broader else 'dragon-flight-mode-native')
    config['extra_files']['dragon-flight-mode-cases.json']=ROOT/'.local/class-skills/dragon-flight-mode/cases.json'
    config['extra_families']+=('dragon-flight-mode',)
    config['extra_inputs']+=(Path(__file__).resolve(),ROOT/'tools/reference/dragon-flight-mode-source-evidence.json')
    return config


if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--broader',action='store_true');args=parser.parse_args()
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(**configuration(args.broader))
