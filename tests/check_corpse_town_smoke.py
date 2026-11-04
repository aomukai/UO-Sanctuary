#!/usr/bin/env python3
from pathlib import Path
from check_blood_life_tap import configuration
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    config=configuration('check_corpse_town.py');config['stage']=ROOT/'.local/class-skills/corpse-town-smoke'
    commands=('corpse-town-ce-right','corpse-town-ce-quick','corpse-town-poison-right','corpse-town-poison-quick')
    config.update(extra_families=('corpse-town',),extra_checks=(),extra_live_commands=(),extra_wait_results={},extra_connected_checks=(),extra_target_input_commands=commands,extra_ground_input_commands=(),extra_instant_input_commands=commands,extra_one_reload_families=(),extra_early_reload_families=(),extra_save_results=(),checked_live_commands=(),save_command='save',save_result=None,connected_save=False)
    config['extra_inputs']=(*config['extra_inputs'],Path(__file__).resolve(),ROOT/'tests/check_corpse_town.py')
    check(**config)
