#!/usr/bin/env python3
"""Separate Rabies skill-poison contact with retained learned input/save cohorts."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_assassin_planned_acceptance import paced_clients
from check_druid_weapon_poison import configuration as prior,OPEN
STAGE=ROOT/'.local/class-skills/rabies-poison-contact'
def configuration():
    c=prior();c.update(stage=STAGE,extra_families=(*c['extra_families'],'rabies-poison-contact'),extra_inputs=(*c['extra_inputs'],Path(__file__).resolve(),ROOT/'tools/reference/druid-weapon-poison-native-evidence.json',ROOT/'tools/reference/rabies-poison-contact-source-review.json'))
    c['extra_manifest_names']['rabies-poison-contact']='druid-rabies-fixtures.json'
    return c
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    try:
        with paced_clients():check(**configuration())
    finally:
        for client in OPEN:client.close()
