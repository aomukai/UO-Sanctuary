#!/usr/bin/env python3
from pathlib import Path
from check_blood_life_tap import configuration
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    config=configuration('check_revive_equipment_pools.py');config['stage']=ROOT/'.local/class-skills/frozen-numeric-admission'
    config['extra_inputs']=(*config['extra_inputs'],Path(__file__).resolve(),ROOT/'tests/check_revive_equipment_pools.py')
    check(**config)
