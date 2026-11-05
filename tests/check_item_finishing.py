#!/usr/bin/env python3
"""Physical finishing and approved current superior delivery with retained scopes."""
import argparse
import os
from pathlib import Path
from check_item_quality import ROOT,configuration as previous
from check_iron_golem_birth import check

def configuration(broader=False,compact=False):
    c=previous(broader,compact)
    c['stage']=ROOT/'.local/class-skills'/('item-finishing-compact'if compact else'item-finishing-broader'if broader else'item-finishing')
    c['extra_families']=('item-finishing',)+c['extra_families']
    c['extra_inputs']+=(Path(__file__).resolve(),)
    c.setdefault('extra_files',{})['item-finishing-cases.json']=ROOT/'.local/class-skills/item-finishing-source/cases.json'
    c['extra_files']['item-finishing-properties-cases.json']=ROOT/'.local/class-skills/item-finishing-properties-source/cases.json'
    return c
if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--broader',action='store_true');parser.add_argument('--compact',action='store_true');args=parser.parse_args()
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(**configuration(args.broader,args.compact))
