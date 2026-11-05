#!/usr/bin/env python3
"""Retain ordered contacts/drains, frozen Revive events and actual native swings."""
from pathlib import Path
from check_blood_life_tap import configuration
from check_iron_golem_birth import ROOT,check

def settings():
    c=configuration('check_npc_flat_drains_retained.py')
    c['stage']=ROOT/'.local/class-skills/npc-freeze-hit-retained'
    c['extra_families']+=('revive-event','revive-native-swing')
    c['extra_connected_checks']=('revive-native-swing-check','revive-native-swing-reload-check')
    c['extra_wait_results'].update({'revive-native-swing-check':'revive-native-swing-result','revive-native-swing-reload-check':'revive-native-swing-result'})
    c.update(save_command='revive-native-swing-save',save_result='revive-native-swing-save-result',connected_save=True)
    c['extra_inputs']+=(Path(__file__).resolve(),)
    return c
if __name__=='__main__':check(**settings())
