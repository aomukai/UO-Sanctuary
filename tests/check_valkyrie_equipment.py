#!/usr/bin/env python3
"""Actual MonEquip loadouts,new allocator and legacy item/body regressions."""
import argparse,os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_valkyrie_body import configuration as prior

def configuration(retained=False):
 c=prior(retained);c['stage']=ROOT/'.local/class-skills'/('valkyrie-loadout-retained'if retained else'valkyrie-loadout')
 c['extra_families']+=('npc-item-allocation','valkyrie-equipment','generated-npc-item')
 c['extra_files'].update({'npc-item-allocation-cases.json':ROOT/'.local/class-skills/npc-item-allocation-source/cases.json',
  'item-quality-current-cases.json':ROOT/'.local/class-skills/item-quality-current-source/prepared-cases.json',
  'valkyrie-legacy-body-fixtures.json':ROOT/'.local/class-skills/valkyrie-body/valkyrie-body-fixtures.json'})
 c['extra_inputs']+=(Path(__file__).resolve(),)
 return c
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--retained',action='store_true');a=p.parse_args()
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**configuration(a.retained))
