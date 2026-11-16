#!/usr/bin/env python3
"""Player proc callbacks,protected nested item frames and native Fire Ball saves."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_assassin_planned_acceptance import paced_clients
from check_player_item_fireball import configuration as prior
STAGE=ROOT/'.local/class-skills/player-item-proc-frames'
def configuration():
    c=prior();c.update(stage=STAGE,extra_inputs=(*c['extra_inputs'],Path(__file__).resolve(),ROOT/'tools/reference/player-item-fireball-native-evidence.json'))
    return c
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    with paced_clients():check(**configuration())
