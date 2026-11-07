#!/usr/bin/env python3
"""Charged NPC item emission plus existing NPC and player/Mage consumers."""
import argparse,os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_npc_item_emission import configuration as prior

def configuration(retained=False,isolate=False,shared=False):
    if shared:
        from check_npc_item_chain_shared import configuration as previous
        c=previous();c['stage']=ROOT/'.local/class-skills/npc-charged-item-shared'
        c['extra_families']+=('mage-basics',)
        c['extra_live_commands']=(*c.get('extra_live_commands',()),'mage-basics-live')
        c['extra_files']['mage-missile-source-cases.json']=ROOT/'.local/class-skills/mage-missiles-source/cases.json'
    else:
        c=prior(retained,isolate);c['stage']=ROOT/'.local/class-skills'/('npc-charged-item-isolate'if isolate else'npc-charged-item-retained'if retained else'npc-charged-item')
        if isolate:c['extra_families']=()
    if not shared:c['extra_families']+=('npc-charged-item',)
    c['extra_files'].update({'npc-charged-item-cases.json':ROOT/'.local/class-skills/npc-charged-item-source/cases.json',
        'charged-bolt-path-source-probe.json':ROOT/'.local/class-skills/charged-bolt-path-source-probe.json'})
    c['extra_inputs']+=(Path(__file__).resolve(),ROOT/'tools/reference/npc-charged-item-source-evidence.json')
    return c

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--retained',action='store_true');p.add_argument('--isolate',action='store_true');p.add_argument('--shared',action='store_true');a=p.parse_args()
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**configuration(a.retained,a.isolate,a.shared))
