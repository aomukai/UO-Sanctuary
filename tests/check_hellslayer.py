#!/usr/bin/env python3
"""Complete unstripped Hellslayer acquisition,player/learned-golem input and saves."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_assassin_planned_acceptance import paced_clients
from check_player_basic_proc import configuration as prior
STAGE=ROOT/'.local/class-skills/hellslayer'
def configuration():
    c=prior();c.update(stage=STAGE,extra_families=(*c['extra_families'],'hellslayer','hellslayer-readiness'),extra_target_input_commands=(*c['extra_target_input_commands'],'hellslayer-player-left','hellslayer-player-right','hellslayer-golem-right','hellslayer-golem-quick'),extra_input_timeouts={'hellslayer-golem-right':30,'hellslayer-golem-quick':30},extra_files={**c.get('extra_files',{}),'hellslayer-before-readiness.json':ROOT/'tools/reference/named-item-pre-proc-baseline.json'},extra_inputs=(*c['extra_inputs'],Path(__file__).resolve(),ROOT/'tools/reference/player-basic-proc-native-evidence.json',ROOT/'tools/reference/named-item-pre-proc-baseline.json'))
    return c
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    with paced_clients():check(**configuration())
