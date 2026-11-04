#!/usr/bin/env python3
from pathlib import Path
from check_blood_life_tap import configuration
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    config=configuration('check_player_melee_return.py');config['stage']=ROOT/'.local/class-skills/player-melee-return-smoke'
    config.update(extra_families=('shiver-armor','player-melee-return'),extra_checks=(),extra_live_commands=(),extra_wait_results={},extra_connected_checks=(),extra_target_input_commands=(),extra_ground_input_commands=(),extra_instant_input_commands=(),extra_one_reload_families=(),extra_early_reload_families=(),extra_save_results=(),checked_live_commands=(),save_command='save',save_result=None,connected_save=False)
    config['extra_inputs']=(*config['extra_inputs'],Path(__file__).resolve(),ROOT/'tests/check_player_melee_return.py')
    check(**config)
