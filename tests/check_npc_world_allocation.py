#!/usr/bin/env python3
"""Persist the real shared world item/root/child stream alongside retained mechanics."""
import argparse,os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_npc_original_contact import configuration as prior
def configuration(retained=False,isolate=False,shared=False):
 c=prior(retained,isolate,shared)
 c['stage']=ROOT/'.local/class-skills'/('npc-world-allocation-shared'if shared else'npc-world-allocation-isolate'if isolate else'npc-world-allocation-retained'if retained else'npc-world-allocation')
 if isolate:c['extra_families']=()
 if not shared:
  c['extra_families']+=('npc-world-allocation',)
  c['extra_files']['npc-world-allocation-source-cases.json']=ROOT/'.local/class-skills/npc-world-allocation-source/cases.json'
 c['extra_inputs']+=(Path(__file__).resolve(),ROOT/'tools/probe_npc_world_allocation.py')
 return c
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--retained',action='store_true');p.add_argument('--isolate',action='store_true');p.add_argument('--shared',action='store_true');a=p.parse_args()
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**configuration(a.retained,a.isolate,a.shared))
