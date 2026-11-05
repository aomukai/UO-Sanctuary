#!/usr/bin/env python3
"""Full source NPC damage orchestration with retained selected-hand and resource checks."""
import argparse,os
from pathlib import Path
from check_generated_npc_physical import ROOT,configuration as previous
from check_iron_golem_birth import check

def configuration(broader=False,compact=False):
    c=previous(broader,compact)
    c['stage']=ROOT/'.local/class-skills'/('npc-damage-fill-compact'if compact else'npc-damage-fill-broader'if broader else'npc-damage-fill')
    families=list(c['extra_families']);families.insert(families.index('generated-npc-stats')+1,'npc-damage-fill');c['extra_families']=tuple(families)
    c['extra_inputs']+=(Path(__file__).resolve(),)
    c['extra_files']['npc-damage-fill-cases.json']=ROOT/'.local/class-skills/npc-damage-fill-source/cases.json'
    return c
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--broader',action='store_true');p.add_argument('--compact',action='store_true');a=p.parse_args()
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(**configuration(a.broader,a.compact))
