#!/usr/bin/env python3
"""Shared item-affix outer orchestration with retained Shadow/Valk and learned cohorts."""
import argparse,os
from pathlib import Path
from check_shadow_equipment import ROOT,configuration as previous
from check_iron_golem_birth import check
def configuration(broader=False):
    c=previous(broader);c['stage']=ROOT/'.local/class-skills'/('item-affixes-broader' if broader else 'item-affixes')
    c['extra_files']['item-affix-cases.json']=ROOT/'.local/class-skills/item-affix-orchestration-source/cases.json'
    c['extra_families']+=('item-affix',)
    c['extra_inputs']+=(Path(__file__).resolve(),ROOT/'tools/reference/item-affix-orchestration-source-evidence.json')
    return c
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--broader',action='store_true');a=p.parse_args()
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**configuration(a.broader))
