#!/usr/bin/env python3
"""Current and historical Mage spell impacts retain typed Health Link context."""
import os,sys
from pathlib import Path
from check_necro_spell_link import configuration as parent_configuration
from check_iron_golem_birth import ROOT,check
from check_assassin_planned_acceptance import paced_clients
STAGE=ROOT/'.local/class-skills/mage-spell-link'
def configuration():
    c=parent_configuration();c['stage']=STAGE;c['extra_families']+=('mage-basics','mage-missile','mage-glacial')
    c['extra_files'].update({'mage-missile-source-cases.json':ROOT/'.local/class-skills/mage-missiles-source/cases.json'})
    c['extra_inputs']+=(Path(__file__).resolve(),ROOT/'tools/reference/necro-spell-link-native-evidence.json')
    return c
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    with paced_clients():check(**configuration())
