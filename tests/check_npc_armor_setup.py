#!/usr/bin/env python3
from pathlib import Path
from check_blood_life_tap import configuration
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    config=configuration('check_npc_defense_conditions.py');config['stage']=ROOT/'.local/class-skills/npc-armor-setup'
    config['extra_families']=(*config['extra_families'],'npc-armor-setup')
    config['extra_live_commands']=(*config['extra_live_commands'],'npc-armor-setup-live')
    config.setdefault('extra_files',{})['npc-armor-setup-source-cases.json']=ROOT/'.local/class-skills/npc-armor-setup-source/cases.json'
    config['extra_inputs']=(*config['extra_inputs'],Path(__file__).resolve(),ROOT/'tests/check_npc_defense_conditions.py')
    check(**config)
