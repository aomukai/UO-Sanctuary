#!/usr/bin/env python3
"""Retain combined resources/native A1,real item curses and live blind AI."""
from pathlib import Path
from check_blood_life_tap import configuration
from check_iron_golem_birth import ROOT,check

def settings():
    c=configuration('check_npc_drain_reload.py');c['stage']=ROOT/'.local/class-skills/npc-item-blind-retained'
    c['extra_families']+=('blood-life-tap','item-effect-cast','blind-world')
    c['extra_checks']+=('blind-policy-check',)
    c['extra_live_commands']=(*c.get('extra_live_commands',()),'blind-world-live','blood-life-tap-live')
    c['extra_files'].update({'npc-hit-skill-source-cases.json':ROOT/'.local/class-skills/npc-hit-skill-source/cases.json','item-effect-cast-cases.json':ROOT/'.local/class-skills/item-effect-casts-source/cases.json','blind-policy-source-probe.json':ROOT/'.local/class-skills/blind-policy-source-probe.json'})
    c['extra_inputs']+=(Path(__file__).resolve(),)
    return c
if __name__=='__main__':check(**settings())
