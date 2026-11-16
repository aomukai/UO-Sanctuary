#!/usr/bin/env python3
"""Persistent original proc providers/player streams and actual equipment events."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_assassin_planned_acceptance import paced_clients
from check_player_item_proc_frames import configuration as prior
STAGE=ROOT/'.local/class-skills/player-item-proc-host'
def configuration():
    c=prior();c.update(stage=STAGE,extra_families=(*c['extra_families'],'player-item-proc-host'),extra_inputs=(*c['extra_inputs'],Path(__file__).resolve(),ROOT/'tools/reference/player-item-proc-frames-native-evidence.json'))
    return c
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    with paced_clients():check(**configuration())
