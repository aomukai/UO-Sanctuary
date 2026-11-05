#!/usr/bin/env python3
"""Prepared quality/automagic composition with retained class regressions."""
import argparse
import os
from pathlib import Path
from check_current_item_affixes import ROOT,configuration as previous
from check_iron_golem_birth import check

def configuration(broader=False,compact=False):
    c=previous(broader,compact)
    c['stage']=ROOT/'.local/class-skills'/('item-quality-current-compact'if compact else'item-quality-current-broader'if broader else'item-quality-current')
    c['extra_families']=('item-quality-current',)+c['extra_families']
    c['extra_inputs']+=(Path(__file__).resolve(),)
    c['extra_files']['item-quality-current-cases.json']=ROOT/'.local/class-skills/item-quality-current-source/prepared-cases.json'
    return c
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--broader',action='store_true');p.add_argument('--compact',action='store_true');a=p.parse_args()
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(**configuration(a.broader,a.compact))
