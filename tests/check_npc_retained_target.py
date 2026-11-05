#!/usr/bin/env python3
"""Retained logical target through reflection and native corpse identity."""
import argparse
import os
from pathlib import Path
from check_npc_incoming_lethal import ROOT,configuration as previous
from check_iron_golem_birth import check

def configuration(broader=False,compact=False):
    c=previous(broader,compact)
    c['stage']=ROOT/'.local/class-skills'/('npc-retained-target-compact'if compact else'npc-retained-target-broader'if broader else'npc-retained-target')
    c['extra_families']=('npc-retained-target',)+c['extra_families']
    c['extra_inputs']+=(Path(__file__).resolve(),)
    return c
if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--broader',action='store_true');parser.add_argument('--compact',action='store_true');args=parser.parse_args()
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(**configuration(args.broader,args.compact))
