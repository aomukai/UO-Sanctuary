#!/usr/bin/env python3
"""Retain shared incoming combat,blind equipment and original Revive cast gates."""
from pathlib import Path
from check_npc_item_blind_retained import settings as previous
from check_iron_golem_birth import ROOT,check

def settings():
    c=previous();c['stage']=ROOT/'.local/class-skills/npc-lightning-return-retained'
    c['extra_families']+=('npc-item-blind','revive-equipped')
    c['extra_connected_checks']=(*c.get('extra_connected_checks',()),'npc-item-blind-check','npc-item-blind-reload-check')
    c['extra_files']['npc-item-blind-cases.json']=ROOT/'.local/class-skills/npc-item-blind-source/cases.json'
    c['extra_inputs']+=(Path(__file__).resolve(),)
    return c

if __name__=='__main__':check(**settings())
