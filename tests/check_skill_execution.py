#!/usr/bin/env python3
"""Common executor with retained item-cast/proc cohorts and actual Blood casts."""
import argparse,os
from pathlib import Path
from check_item_effect_casts import ROOT,configuration as previous
from check_iron_golem_birth import check

def configuration(broader=False):
    c=previous(broader);c['stage']=ROOT/'.local/class-skills'/('skill-execution-broader' if broader else 'skill-execution')
    c['extra_files']['skill-execution-cases.json']=ROOT/'.local/class-skills/skill-execution-source/cases.json'
    c['extra_families']=('skill-execution',)+c['extra_families']
    c['extra_inputs']+=(Path(__file__).resolve(),ROOT/'tools/reference/skill-execution-source-evidence.json')
    return c
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--broader',action='store_true');a=p.parse_args();os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**configuration(a.broader))
