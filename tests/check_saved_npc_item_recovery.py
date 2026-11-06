#!/usr/bin/env python3
"""Original NPC kill recovery with saved class-skill/socket projection."""
import argparse,os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_npc_kill_recovery import configuration as prior

def configuration(retained=False):
    c=prior(retained)
    c['stage']=ROOT/'.local/class-skills'/('saved-npc-item-recovery-retained'if retained else'saved-npc-item-recovery')
    c['extra_families']+=('item-property-writers','iron-golem-birth-graph','item-property-creation')
    c['extra_checks']=(*c.get('extra_checks',()),'saved-item-special-properties-check')
    c['extra_inputs']+=(Path(__file__).resolve(),)
    c['extra_files'].update({'saved-socket-property-cases.json':ROOT/'.local/class-skills/saved-socket-property-source/cases.json',
        'item-property-writers-source-probe.json':ROOT/'.local/class-skills/item-property-writers-source-probe.json',
        'iron-golem-birth-graph-source-cases.json':ROOT/'.local/class-skills/iron-golem-birth-graph-source/cases.json',
        'item-property-creation-cases.json':ROOT/'.local/class-skills/item-property-creation-source/cases.json'})
    return c

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--retained',action='store_true');a=p.parse_args()
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**configuration(a.retained))
