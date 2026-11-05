#!/usr/bin/env python3
"""Complete quality orchestration and original controlled native graphs."""
import argparse
import os
from pathlib import Path
from check_item_initialization import ROOT,configuration as previous
from check_iron_golem_birth import check

def configuration(broader=False,compact=False):
    c=previous(broader,compact)
    c['stage']=ROOT/'.local/class-skills'/('item-quality-compact'if compact else'item-quality-broader'if broader else'item-quality')
    c['extra_families']=('item-quality',)+c['extra_families']
    c['extra_inputs']+=(Path(__file__).resolve(),)
    c.setdefault('extra_files',{})['item-quality-cases.json']=ROOT/'.local/class-skills/item-quality-source/cases.json'
    return c
if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--broader',action='store_true');parser.add_argument('--compact',action='store_true');args=parser.parse_args()
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(**configuration(args.broader,args.compact))
