#!/usr/bin/env python3
"""Source body/hand placement and native generated item delivery."""
import argparse
import os
from pathlib import Path
from check_generated_npc_item import ROOT,configuration as previous
from check_iron_golem_birth import check

def configuration(broader=False,compact=False):
    c=previous(broader,compact)
    c['stage']=ROOT/'.local/class-skills'/('npc-equip-placement-compact'if compact else'npc-equip-placement-broader'if broader else'npc-equip-placement')
    c['extra_families']=('npc-equip-placement',)+c['extra_families']
    c['extra_inputs']+=(Path(__file__).resolve(),)
    c['extra_files']['npc-equip-placement-cases.json']=ROOT/'.local/class-skills/npc-equip-placement-source/cases.json'
    return c
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--broader',action='store_true');p.add_argument('--compact',action='store_true');a=p.parse_args()
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(**configuration(a.broader,a.compact))
