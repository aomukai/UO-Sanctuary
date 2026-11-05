#!/usr/bin/env python3
"""Source item stat dispatch and original native multi-item numeric graphs."""
import argparse
import os
from pathlib import Path
from check_npc_equip_placement import ROOT,configuration as previous
from check_iron_golem_birth import check

def configuration(broader=False,compact=False):
    c=previous(broader,compact)
    c['stage']=ROOT/'.local/class-skills'/('generated-npc-stats-compact'if compact else'generated-npc-stats-broader'if broader else'generated-npc-stats')
    c['extra_families']=('generated-npc-stats',)+c['extra_families']
    c['extra_inputs']+=(Path(__file__).resolve(),)
    c['extra_files']['generated-npc-stats-cases.json']=ROOT/'.local/class-skills/generated-npc-stats-source/cases.json'
    return c
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--broader',action='store_true');p.add_argument('--compact',action='store_true');a=p.parse_args()
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(**configuration(a.broader,a.compact))
