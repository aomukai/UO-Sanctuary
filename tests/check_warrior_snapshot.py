#!/usr/bin/env python3
"""Warrior hard-point birth snapshots with original actors and class regressions."""
import argparse
import os
from pathlib import Path
from check_creeper_learned import ROOT,configuration as previous
from check_iron_golem_birth import check


def configuration(broader=False):
    c=previous(broader)
    c['stage']=ROOT/'.local/class-skills'/('warrior-snapshot-broader' if broader else 'warrior-snapshot')
    c['extra_families']=('warrior-snapshot','skeleton-upgrades')+c['extra_families']
    c['extra_live_commands']=('skeleton-upgrades-live',)+c['extra_live_commands']
    c['extra_inputs']+=(Path(__file__).resolve(),)
    return c


if __name__=='__main__':
    parser=argparse.ArgumentParser()
    parser.add_argument('--broader',action='store_true')
    args=parser.parse_args()
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(**configuration(args.broader))
