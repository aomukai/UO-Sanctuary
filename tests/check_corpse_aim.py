#!/usr/bin/env python3
from pathlib import Path
from check_blood_life_tap import configuration
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    config=configuration('check_bone_prison_aim.py');config['stage']=ROOT/'.local/class-skills/corpse-aim'
    config['extra_families']=(*config['extra_families'],'corpse-cast','corpse-aim')
    config.setdefault('extra_manifest_names',{})['corpse-cast']='corpse-explosion-cast-fixtures.json'
    config['extra_files']['necro-corpse-explosion-source-probe.json']=ROOT/'.local/class-skills/necro-corpse-explosion-source-probe.json'
    config['extra_target_input_commands']=(*config['extra_target_input_commands'],'corpse-ground-right','corpse-ground-quick')
    config['extra_inputs']=(*config['extra_inputs'],Path(__file__).resolve(),ROOT/'tests/check_bone_prison_aim.py')
    check(**config)
