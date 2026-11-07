#!/usr/bin/env python3
"""Verify actual source-initialized Valkyrie creation and retained class consumers."""
import argparse,os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_npc_world_allocation import configuration as prior
def configuration(retained=False,isolate=False,shared=False):
 c=prior(retained,isolate,shared)
 c['stage']=ROOT/'.local/class-skills'/('valkyrie-initializer-shared'if shared else'valkyrie-initializer-isolate'if isolate else'valkyrie-initializer-retained'if retained else'valkyrie-initializer')
 if isolate:c['extra_families']=()
 if not shared:
  c['extra_families']+=('valkyrie-initializer',)
  c['extra_pre_save_commands']=(*c.get('extra_pre_save_commands',()),'valkyrie-initializer-seal')
  c['extra_files']['valkyrie-initializer-source-cases.json']=ROOT/'.local/class-skills/valkyrie-initializer-source/cases.json'
 c['extra_inputs']+=(Path(__file__).resolve(),ROOT/'tools/probe_valkyrie_initializer.py',ROOT/'tools/corroborate_valkyrie_initializer.py')
 return c
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--retained',action='store_true');p.add_argument('--isolate',action='store_true');p.add_argument('--shared',action='store_true');a=p.parse_args()
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**configuration(a.retained,a.isolate,a.shared))
