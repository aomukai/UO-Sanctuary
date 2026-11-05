#!/usr/bin/env python3
"""Current magic/rare composition with retained staffmod/class regression."""
import argparse
import os
from pathlib import Path
from check_item_staffmods import ROOT,configuration as previous
from check_iron_golem_birth import check

def configuration(broader=False,compact=False):
    c=previous(broader,compact)
    c['stage']=ROOT/'.local/class-skills'/('current-item-affixes-compact'if compact else'current-item-affixes-broader'if broader else'current-item-affixes')
    c['extra_families']=('current-item-affixes',)+c['extra_families']
    c['extra_inputs']+=(Path(__file__).resolve(),)
    c['extra_files']['current-item-affixes-cases.json']=ROOT/'.local/class-skills/current-item-affixes-source/cases.json'
    return c
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--broader',action='store_true');p.add_argument('--compact',action='store_true');a=p.parse_args()
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(**configuration(a.broader,a.compact))
