#!/usr/bin/env python3
from pathlib import Path
from check_blood_life_tap import configuration
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    config=configuration('check_frozen_numeric_admission.py');config['stage']=ROOT/'.local/class-skills/revive-equipment-combat'
    config['extra_families']=(*config['extra_families'],'revive-equipment-combat');config['extra_inputs']=(*config['extra_inputs'],Path(__file__).resolve(),ROOT/'tests/check_frozen_numeric_admission.py')
    check(**config)
