#!/usr/bin/env python3
"""Original generated NPC selected-hand graphs with retained live resources."""
import argparse,os
from pathlib import Path
from check_generated_npc_live_resources import ROOT,configuration as previous
from check_iron_golem_birth import check

def configuration(broader=False,compact=False):
    c=previous(broader,compact)
    c['stage']=ROOT/'.local/class-skills'/('generated-npc-weapons-compact'if compact else'generated-npc-weapons-broader'if broader else'generated-npc-weapons')
    c['extra_families']+=('generated-npc-weapons',)
    c['extra_inputs']+=(Path(__file__).resolve(),)
    c['extra_files']['generated-npc-weapons-cases.json']=ROOT/'.local/class-skills/generated-npc-weapons-source/cases.json'
    c['extra_files']['generated-npc-weapon-selector-cases.json']=ROOT/'.local/class-skills/dragon-talon-hands-source-probe.json'
    return c
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--broader',action='store_true');p.add_argument('--compact',action='store_true');a=p.parse_args()
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(**configuration(a.broader,a.compact))
