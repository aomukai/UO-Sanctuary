#!/usr/bin/env python3
"""Hunger/Rabies/Fury action-time item procs with retained class and paid-save proofs."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_assassin_planned_acceptance import paced_clients
from check_druid_captured_proc import configuration as prior
from check_druid_planned_acceptance import add_fixture,fury_save,OPEN
STAGE=ROOT/'.local/class-skills/druid-action-proc'
def configuration():
    c=prior();c.update(stage=STAGE)
    for f in ('DruidHunger','DruidRabies','DruidFury'):add_fixture(c,f)
    c.update(extra_families=(*c['extra_families'],'druid-action-proc'),extra_target_input_commands=(*c['extra_target_input_commands'],*(f'druid-action-proc-{skill}-{mode}' for skill in ('hunger-wolf','hunger-bear','rabies','fury') for mode in ('right','quick'))),extra_pre_save_commands=(*c['extra_pre_save_commands'],'druid-hunger-snapshot-check','druid-rabies-snapshot-check','druid-fury-snapshot-check'),extra_pre_save_callbacks=(*c.get('extra_pre_save_callbacks',()),fury_save),extra_inputs=(*c['extra_inputs'],Path(__file__).resolve(),ROOT/'tools/reference/druid-captured-proc-native-evidence.json'))
    return c
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    try:
        with paced_clients():check(**configuration())
    finally:
        for client in OPEN:client.close()
