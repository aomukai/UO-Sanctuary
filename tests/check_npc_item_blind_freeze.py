#!/usr/bin/env python3
"""Retain learned original freeze equipment beside new blind equipment."""
from pathlib import Path
from check_blood_life_tap import configuration
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    c=configuration('check_npc_freeze_hit.py');c['stage']=ROOT/'.local/class-skills/npc-item-blind-freeze'
    c['extra_inputs']+=(Path(__file__).resolve(),)
    check(**c)
