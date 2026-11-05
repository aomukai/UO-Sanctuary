#!/usr/bin/env python3
"""Item-effect casting with actual Blood casts and retained proc/property cohorts."""
import argparse
import os
from pathlib import Path
from check_item_proc_callbacks import ROOT, configuration as previous
from check_iron_golem_birth import check

def configuration(broader=False):
    c=previous(broader)
    c['stage']=ROOT/'.local/class-skills'/('item-effect-casts-broader' if broader else 'item-effect-casts')
    c['extra_files']['item-effect-cast-cases.json']=ROOT/'.local/class-skills/item-effect-casts-source/cases.json'
    c['extra_families']=('item-effect-cast',)+c['extra_families']
    c['extra_inputs']+=(Path(__file__).resolve(),ROOT/'tools/reference/item-effect-casts-source-evidence.json')
    return c
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--broader',action='store_true');a=p.parse_args()
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**configuration(a.broader))
