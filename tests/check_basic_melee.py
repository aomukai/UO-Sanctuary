#!/usr/bin/env python3
"""Eight-class ordinary melee without proc equipment,forms and mana fallbacks."""
import os,sys
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_assassin_planned_acceptance import paced_clients
from check_druid_setup_items import configuration as prior,OPEN
STAGE=ROOT/'.local/class-skills/basic-melee'
def configuration():
    c=prior();classes=('amazon','sorceress','necromancer','paladin','barbarian','druid','assassin','warlock')
    c.update(stage=STAGE,extra_families=(*c['extra_families'],'basic-melee'),extra_target_input_commands=(*c['extra_target_input_commands'],*(f'basic-melee-{cls}-{side}' for cls in classes for side in ('left','right'))),extra_inputs=(*c['extra_inputs'],Path(__file__).resolve(),ROOT/'tools/reference/druid-setup-items-native-evidence.json'))
    return c
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    try:
        with paced_clients():check(**configuration())
    finally:
        for client in OPEN:client.close()
