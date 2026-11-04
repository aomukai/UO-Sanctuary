#!/usr/bin/env python3
"""Current Flight sequence planning with retained learned Tail and shared controls."""
import argparse
import os
from pathlib import Path
from check_dragon_tail import ROOT, configuration as previous
from check_iron_golem_birth import check


def configuration(broader=False):
    config=previous(broader)
    config['stage']=ROOT/'.local/class-skills'/('dragon-flight-preparation-broader' if broader else 'dragon-flight-preparation')
    config['extra_files']['dragon-flight-source-cases.json']=ROOT/'.local/class-skills/dragon-flight-source/cases.json'
    config['extra_families']+=('dragon-flight-sequence','cleave','mirrored-blades')
    config['extra_inputs']+=(Path(__file__).resolve(),ROOT/'tools/reference/dragon-flight-source-evidence.json')
    return config


if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--broader',action='store_true');args=parser.parse_args()
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(**configuration(args.broader))
