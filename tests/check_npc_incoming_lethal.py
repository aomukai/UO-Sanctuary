#!/usr/bin/env python3
"""Lethal incoming receiver and retired-caster persistence with retained scopes."""
import argparse
import os
from pathlib import Path
from check_npc_incoming_procs import ROOT,configuration as previous
from check_iron_golem_birth import check

def configuration(broader=False,compact=False):
    c=previous(broader,compact)
    c['stage']=ROOT/'.local/class-skills'/('npc-incoming-lethal-compact'if compact else'npc-incoming-lethal-broader'if broader else'npc-incoming-lethal')
    c['extra_families']=('npc-incoming-lethal',)+c['extra_families']
    c['extra_inputs']+=(Path(__file__).resolve(),)
    return c
if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--broader',action='store_true');parser.add_argument('--compact',action='store_true');args=parser.parse_args()
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(**configuration(args.broader,args.compact))
