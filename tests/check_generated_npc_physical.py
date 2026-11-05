#!/usr/bin/env python3
"""Original generated items: selected-hand physical rolls and native reloads."""
import argparse,os
from pathlib import Path
from check_generated_npc_weapons import ROOT,configuration as previous
from check_iron_golem_birth import check

def configuration(broader=False,compact=False):
    c=previous(broader,compact)
    c['stage']=ROOT/'.local/class-skills'/('generated-npc-physical-stats-compact'if compact else'generated-npc-physical-stats-broader'if broader else'generated-npc-physical-stats')
    c['extra_families']+=('generated-npc-physical',)
    c['extra_inputs']+=(Path(__file__).resolve(),)
    c['extra_files']['generated-npc-physical-cases.json']=ROOT/'.local/class-skills/generated-npc-physical-stats-source/cases.json'
    return c
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--broader',action='store_true');p.add_argument('--compact',action='store_true');a=p.parse_args()
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(**configuration(a.broader,a.compact))
