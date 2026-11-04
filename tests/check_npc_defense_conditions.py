#!/usr/bin/env python3
from pathlib import Path
from check_blood_life_tap import configuration
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    config=configuration('check_npc_prevent_heal.py');config['stage']=ROOT/'.local/class-skills/npc-defense-conditions'
    config['extra_families']=(*config['extra_families'],'npc-defense-conditions')
    config['extra_inputs']=(*config['extra_inputs'],Path(__file__).resolve(),ROOT/'tests/check_npc_prevent_heal.py')
    check(**config)
