#!/usr/bin/env python3
"""Live original generated-item resource profiles and native reloads."""
import argparse,os
from pathlib import Path
from check_generated_npc_resources import ROOT,configuration as previous
from check_iron_golem_birth import check

def configuration(broader=False,compact=False):
    c=previous(broader,compact)
    c['stage']=ROOT/'.local/class-skills'/('generated-npc-live-resources-compact'if compact else'generated-npc-live-resources-broader'if broader else'generated-npc-live-resources')
    c['extra_families']=('generated-npc-live-resources',)+c['extra_families']
    c['extra_inputs']+=(Path(__file__).resolve(),)
    return c
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--broader',action='store_true');p.add_argument('--compact',action='store_true');a=p.parse_args()
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(**configuration(a.broader,a.compact))
