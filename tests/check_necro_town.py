#!/usr/bin/env python3
from pathlib import Path
from check_blood_life_tap import configuration
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    config=configuration('check_corpse_aim.py');config['stage']=ROOT/'.local/class-skills/necro-town'
    config['extra_families']=(*config['extra_families'],'necro-town')
    config['extra_target_input_commands']=(*config['extra_target_input_commands'],'necro-town-right','necro-town-quick','necro-town-armor-quick')
    config['extra_instant_input_commands']=(*config.get('extra_instant_input_commands',()),'necro-town-right','necro-town-quick')
    config['extra_inputs']=(*config['extra_inputs'],Path(__file__).resolve(),ROOT/'tests/check_corpse_aim.py')
    check(**config)
