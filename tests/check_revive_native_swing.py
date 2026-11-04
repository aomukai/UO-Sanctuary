#!/usr/bin/env python3
from pathlib import Path
from check_blood_life_tap import configuration
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    config=configuration('check_revive_swing_clock.py');config['stage']=ROOT/'.local/class-skills/revive-native-swing'
    config['extra_families']=(*config['extra_families'],'revive-native-swing')
    config['extra_connected_checks']=(*config.get('extra_connected_checks',()),'revive-native-swing-check','revive-native-swing-reload-check')
    config.setdefault('extra_wait_results',{}).update({'revive-native-swing-check':'revive-native-swing-result','revive-native-swing-reload-check':'revive-native-swing-result'})
    config['extra_save_results']=(*config.get('extra_save_results',()),config['save_result'])
    config.update(save_command='revive-native-swing-save',save_result='revive-native-swing-save-result',connected_save=True)
    config['extra_inputs']=(*config['extra_inputs'],Path(__file__).resolve(),ROOT/'tests/check_revive_swing_clock.py')
    check(**config)
