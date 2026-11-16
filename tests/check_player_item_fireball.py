#!/usr/bin/env python3
"""Prepared player item Fire Ball;retain learned and original-golem consumers."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_assassin_planned_acceptance import paced_clients
STAGE=ROOT/'.local/class-skills/player-item-fireball'
def configuration():
    return dict(stage=STAGE,extra_families=('player-item-fireball','fire-ball','iron-golem-fireball'),extra_connected_checks=('fire-ball-reload-check',),extra_wait_results={'fire-ball-reload-check':'fire-ball-reload-result'},extra_target_input_commands=('fire-ball-right','fire-ball-quick'),extra_connection_settle=.15,extra_inputs=(Path(__file__).resolve(),ROOT/'tests/check_assassin_planned_acceptance.py',ROOT/'tools/reference/npc-item-fireball-native-evidence.json',ROOT/'tools/reference/sorceress-mastery-grants-native-evidence.json'))
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    with paced_clients():check(**configuration())
