#!/usr/bin/env python3
"""Approved player melee proc world rule with authenticated learned Zeal input."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_assassin_planned_acceptance import paced_clients
from check_player_item_proc_world import configuration as prior
STAGE=ROOT/'.local/class-skills/player-item-proc-live'
def configuration():
    c=prior();c.update(stage=STAGE,extra_target_input_commands=(*c['extra_target_input_commands'],'player-item-proc-zeal-right','player-item-proc-zeal-quick'),extra_inputs=(*c['extra_inputs'],Path(__file__).resolve(),ROOT/'tools/reference/player-item-proc-world-native-evidence.json'))
    return c
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    with paced_clients():check(**configuration())
