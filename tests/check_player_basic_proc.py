#!/usr/bin/env python3
"""Eight-class ordinary melee item procs with authenticated left/right input."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_assassin_planned_acceptance import paced_clients
from check_original_item_proc_projection import configuration as prior
STAGE=ROOT/'.local/class-skills/player-basic-proc'
def configuration():
    c=prior();c.update(stage=STAGE,extra_families=(*c['extra_families'],'player-basic-proc'),extra_target_input_commands=(*c['extra_target_input_commands'],'player-basic-proc-left','player-basic-proc-right'),extra_inputs=(*c['extra_inputs'],Path(__file__).resolve(),ROOT/'tools/reference/original-item-proc-projection-native-evidence.json'))
    return c
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    with paced_clients():check(**configuration())
