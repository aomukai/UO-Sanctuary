#!/usr/bin/env python3
"""Approved saved Pierce action mapping through ordinary Amazon characters."""
import os,sys
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_assassin_planned_acceptance import paced_clients
from check_pierce_action_policy import configuration as prior
STAGE=ROOT/'.local/class-skills/pierce-action-adoption'
def configuration():
    c=prior();c['stage']=STAGE;c['extra_inputs']+=(Path(__file__).resolve(),ROOT/'tools/reference/pierce-action-policy-approval.json',ROOT/'tools/reference/assassin-spell-link-native-evidence.json',ROOT/'tools/build_skill_roadmap.py')
    return c
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    with paced_clients():check(**configuration())
