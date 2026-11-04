#!/usr/bin/env python3
from pathlib import Path
from check_blood_life_tap import configuration
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    config=configuration('check_necro_town.py');config['stage']=ROOT/'.local/class-skills/poison-corpse-aim'
    config['extra_families']=(*config['extra_families'],'poison-explosion','poison-corpse-aim')
    config['extra_files'].update({
        'poison-explosion-damage-source-cases.json':ROOT/'.local/class-skills/poison-explosion-damage/cases.json',
        'poison-explosion-emitter-source-cases.json':ROOT/'.local/class-skills/poison-explosion-emitter/cases.json'})
    config['extra_target_input_commands']=(*config['extra_target_input_commands'],'poison-explosion-right','poison-explosion-quick','poison-ground-right','poison-ground-quick')
    config['extra_inputs']=(*config['extra_inputs'],Path(__file__).resolve(),ROOT/'tests/check_necro_town.py')
    check(**config)
