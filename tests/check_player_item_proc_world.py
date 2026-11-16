#!/usr/bin/env python3
"""Ordered player melee proc callers with the existing saved world seed stream."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_assassin_planned_acceptance import paced_clients
from check_saved_item_proc_properties import configuration as prior
STAGE=ROOT/'.local/class-skills/player-item-proc-world'
def configuration():
    c=prior();c.update(stage=STAGE,extra_families=(*c['extra_families'],'player-item-proc-world'),extra_early_reload_families=('player-item-proc-world',),extra_pre_save_commands=('player-item-proc-world-save-check',),extra_inputs=(*c['extra_inputs'],Path(__file__).resolve(),ROOT/'tools/reference/saved-item-proc-properties-native-evidence.json'))
    return c
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    with paced_clients():check(**configuration())
