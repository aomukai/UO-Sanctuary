#!/usr/bin/env python3
from pathlib import Path
from check_blood_life_tap import configuration
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    config=configuration('check_revive_native_swing_focus.py');config['stage']=ROOT/'.local/class-skills/revive-equipped-focus'
    config['extra_families']=(*config['extra_families'],'revive-equipped')
    config['extra_target_input_commands']=('revive-equipped-right','revive-equipped-quick')
    config['extra_inputs']=(*config['extra_inputs'],Path(__file__).resolve(),ROOT/'tests/check_revive_native_swing_focus.py')
    check(**config)
