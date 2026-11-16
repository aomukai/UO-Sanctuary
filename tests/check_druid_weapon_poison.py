#!/usr/bin/env python3
"""Source item-poison allocation on learned form skills,actual inputs and saved clocks."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_assassin_planned_acceptance import paced_clients
from check_basic_melee import configuration as prior,OPEN
STAGE=ROOT/'.local/class-skills/druid-weapon-poison'
def configuration():
    c=prior();c.update(stage=STAGE,extra_families=(*c['extra_families'],'druid-weapon-poison'),extra_target_input_commands=(*c['extra_target_input_commands'],*(f'druid-weapon-poison-{skill}-{mode}' for skill in ('maul','hunger','rabies','fury') for mode in ('right','quick'))),extra_inputs=(*c['extra_inputs'],Path(__file__).resolve(),ROOT/'tools/reference/basic-melee-native-evidence.json',ROOT/'tools/reference/poison-dagger-source-evidence.json',ROOT/'tools/reference/venom-damage-engine-evidence.json'))
    return c
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    try:
        with paced_clients():check(**configuration())
    finally:
        for client in OPEN:client.close()
