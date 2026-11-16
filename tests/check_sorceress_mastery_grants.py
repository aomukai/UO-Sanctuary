#!/usr/bin/env python3
"""Five complete originals,untrained Fire Bolt and actual item-mastery life damage."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_assassin_planned_acceptance import paced_clients
from check_sorceress_staff_grants import configuration as prior
STAGE=ROOT/'.local/class-skills/sorceress-mastery-grants'
def configuration():
    c=prior()
    c.update(stage=STAGE,extra_target_input_commands=('sorc-staff-firebolt-right','sorc-staff-firebolt-quick'),extra_ground_input_commands=(),extra_inputs=(*c['extra_inputs'],Path(__file__).resolve(),ROOT/'tools/reference/sorceress-staff-grants-native-evidence.json'))
    return c
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    with paced_clients():check(**configuration())
