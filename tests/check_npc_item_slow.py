#!/usr/bin/env python3
"""Approved shared pet slow through original items,Clay and existing event paths."""
import argparse,os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_saved_npc_item_recovery import configuration as prior

def configuration(retained=False):
    c=prior(True)if retained else dict(extra_families=(),extra_inputs=(),extra_connected_checks=(),extra_files={})
    c['stage']=ROOT/'.local/class-skills'/('npc-item-slow-retained'if retained else'npc-item-slow')
    c['extra_families']+=('npc-item-slow',)
    c['extra_connected_checks']+=('npc-item-slow-check','npc-item-slow-reload-check')
    c['extra_files']['pet-slow-velocity-cases.json']=ROOT/'.local/class-skills/pet-slow-velocity-source/cases.json'
    c['extra_inputs']+=(Path(__file__).resolve(),)
    return c
if __name__=='__main__':
    p=argparse.ArgumentParser();g=p.add_mutually_exclusive_group();g.add_argument('--retained',action='store_true');g.add_argument('--clay',action='store_true');a=p.parse_args()
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));c=configuration(a.retained)
    if a.clay:
        from check_clay_golem_world import configuration as clay
        c=clay();c['stage']=ROOT/'.local/class-skills/npc-item-slow-clay';c['extra_families']+=('npc-item-slow',)
        c['extra_connected_checks']=('npc-item-slow-check','npc-item-slow-reload-check');c['extra_inputs']+=(Path(__file__).resolve(),)
        c['extra_files']['pet-slow-velocity-cases.json']=ROOT/'.local/class-skills/pet-slow-velocity-source/cases.json'
    check(**c)
