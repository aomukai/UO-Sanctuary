#!/usr/bin/env python3
from pathlib import Path
from check_blood_life_tap import configuration
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    config=configuration('check_npc_armor_setup_focus.py');config['stage']=ROOT/'.local/class-skills/npc-iron-maiden-focus'
    config['extra_families']=(*config['extra_families'],'npc-iron-maiden')
    config['extra_live_commands']=(*config.get('extra_live_commands',()),'npc-iron-maiden-live')
    config['extra_inputs']=(*config['extra_inputs'],Path(__file__).resolve(),ROOT/'tests/check_npc_armor_setup_focus.py')
    check(**config)
