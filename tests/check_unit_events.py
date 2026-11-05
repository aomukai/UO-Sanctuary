#!/usr/bin/env python3
"""Actual item event graphs and retained class/equipment regressions."""
import argparse,os
from pathlib import Path
from check_item_execution_metadata import ROOT,configuration as previous
from check_iron_golem_birth import check

def configuration(broader=False):
    c=previous(broader);c['stage']=ROOT/'.local/class-skills'/('unit-events-broader' if broader else 'unit-events')
    c['extra_files']['unit-events-source-cases.json']=ROOT/'.local/class-skills/unit-events-source/cases.json'
    c['extra_families']=('unit-events',)+c['extra_families']
    c['extra_live_commands']=('unit-events-live',)+c.get('extra_live_commands',())
    c['extra_inputs']+=(Path(__file__).resolve(),ROOT/'tools/reference/unit-events-source-evidence.json')
    return c
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--broader',action='store_true');a=p.parse_args();os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**configuration(a.broader))
