#!/usr/bin/env python3
from pathlib import Path
from check_blood_life_tap import configuration
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    config=configuration('check_revive_melee_geometry.py');config['stage']=ROOT/'.local/class-skills/revive-swing-clock'
    config['extra_families']=(*config['extra_families'],'revive-swing-clock')
    config['extra_files']['revive-attack-clock-source-probe.json']=ROOT/'.local/class-skills/revive-attack-clock-source-probe.json'
    config['extra_inputs']=(*config['extra_inputs'],Path(__file__).resolve(),ROOT/'tests/check_revive_melee_geometry.py')
    check(**config)
