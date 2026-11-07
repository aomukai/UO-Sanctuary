#!/usr/bin/env python3
"""Prepared Talon clock with retained current kick/Tail/Flight components."""
import argparse
import os
from pathlib import Path
from check_iron_golem_birth import ROOT, check
from check_dragon_flight_mode import configuration as previous


def configuration(broader=False, isolate=False):
    if isolate:
        c=dict(extra_files={},extra_families=(),extra_inputs=())
    else:
        c=previous(broader)
    suffix='-isolate' if isolate else '-broader' if broader else ''
    c['stage']=ROOT/('.local/class-skills/dragon-talon-clock'+suffix)
    c['extra_files']['dragon-talon-clock-cases.json']=ROOT/'.local/class-skills/dragon-talon-clock/cases.json'
    c['extra_families']+=('dragon-talon-clock',)
    c['extra_inputs']+=(Path(__file__).resolve(),ROOT/'tools/reference/dragon-talon-clock-source-evidence.json')
    return c


if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--broader',action='store_true');p.add_argument('--isolate',action='store_true');args=p.parse_args()
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(**configuration(args.broader,args.isolate))
