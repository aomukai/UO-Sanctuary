#!/usr/bin/env python3
"""Raw proc/charge writers with retained fresh numeric and summon cohorts."""
import argparse,os
from pathlib import Path
from check_item_property_creation import ROOT,configuration as previous
from check_iron_golem_birth import check
def configuration(broader=False):
    c=previous(broader);c['stage']=ROOT/'.local/class-skills'/('item-skill-properties-broader' if broader else 'item-skill-properties')
    c['extra_files']['item-skill-property-cases.json']=ROOT/'.local/class-skills/item-skill-property-source/cases.json'
    c['extra_families']+=('item-skill-property',)
    c['extra_inputs']+=(Path(__file__).resolve(),ROOT/'tools/reference/item-skill-property-source-evidence.json')
    return c
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--broader',action='store_true');a=p.parse_args()
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**configuration(a.broader))
