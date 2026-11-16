#!/usr/bin/env python3
"""Complete original Werewolf/Werebear basic melee procs and ordinary input."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_assassin_planned_acceptance import paced_clients
from check_hellslayer import configuration as prior
STAGE=ROOT/'.local/class-skills/druid-basic-proc'
def configuration():
    c=prior();c.update(stage=STAGE,extra_families=(*c['extra_families'],'druid-basic-proc'),extra_target_input_commands=(*c['extra_target_input_commands'],*(f'druid-basic-proc-{form}-{side}' for form in ('wolf','bear') for side in ('left','right'))),extra_inputs=(*c['extra_inputs'],Path(__file__).resolve(),ROOT/'tools/reference/hellslayer-native-evidence.json'))
    return c
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    with paced_clients():check(**configuration())
