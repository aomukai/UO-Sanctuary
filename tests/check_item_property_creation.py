#!/usr/bin/env python3
"""Fresh item-owned numeric/layered graphs with retained NPC pool/summon cohorts."""
import argparse,os
from pathlib import Path
from check_npc_item_affixes import ROOT,configuration as previous
from check_iron_golem_birth import check
def configuration(broader=False):
    c=previous(broader);c['stage']=ROOT/'.local/class-skills'/('item-property-creation-broader' if broader else 'item-property-creation')
    c['extra_files']['item-property-creation-cases.json']=ROOT/'.local/class-skills/item-property-creation-source/cases.json'
    c['extra_families']+=('item-property-creation',)
    c['extra_inputs']+=(Path(__file__).resolve(),ROOT/'tools/reference/item-property-creation-source-evidence.json')
    return c
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--broader',action='store_true');a=p.parse_args()
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**configuration(a.broader))
