#!/usr/bin/env python3
from pathlib import Path
from check_blood_life_tap import configuration
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    config=configuration('check_poison_corpse_aim.py');config['stage']=ROOT/'.local/class-skills/corpse-town'
    commands=('corpse-town-ce-right','corpse-town-ce-quick','corpse-town-poison-right','corpse-town-poison-quick')
    config['extra_families']=(*config['extra_families'],'corpse-town')
    config['extra_target_input_commands']=(*config['extra_target_input_commands'],*commands)
    config['extra_instant_input_commands']=(*config['extra_instant_input_commands'],*commands)
    config['extra_inputs']=(*config['extra_inputs'],Path(__file__).resolve(),ROOT/'tests/check_poison_corpse_aim.py')
    check(**config)
