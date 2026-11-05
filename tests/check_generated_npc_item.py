#!/usr/bin/env python3
"""Native generated-item serialization with retained prepared/class checks."""
import argparse
import os
from pathlib import Path
from check_item_quality_current import ROOT,configuration as previous
from check_iron_golem_birth import check

def configuration(broader=False,compact=False):
    c=previous(broader,compact)
    c['stage']=ROOT/'.local/class-skills'/('generated-npc-item-compact'if compact else'generated-npc-item-broader'if broader else'generated-npc-item')
    c['extra_families']=('generated-npc-item',)+c['extra_families']
    c['extra_inputs']+=(Path(__file__).resolve(),)
    return c
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--broader',action='store_true');p.add_argument('--compact',action='store_true');a=p.parse_args()
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(**configuration(a.broader,a.compact))
