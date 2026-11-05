#!/usr/bin/env python3
"""Staffmod source and original graphs with retained finishing/class scopes."""
import argparse
import os
from pathlib import Path
from check_item_finishing import ROOT,configuration as previous
from check_iron_golem_birth import check

def configuration(broader=False,compact=False):
    c=previous(broader,compact)
    c['stage']=ROOT/'.local/class-skills'/('item-staffmods-compact'if compact else'item-staffmods-broader'if broader else'item-staffmods')
    c['extra_families']=('item-staffmods',)+c['extra_families']
    c['extra_inputs']+=(Path(__file__).resolve(),)
    c['extra_files']['item-staffmods-cases.json']=ROOT/'.local/class-skills/item-staffmods-source/cases.json'
    return c
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--broader',action='store_true');p.add_argument('--compact',action='store_true');a=p.parse_args()
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(**configuration(a.broader,a.compact))
