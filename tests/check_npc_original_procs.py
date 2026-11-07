#!/usr/bin/env python3
"""Actual full-original item callback dispatch and saved spell consumers."""
import argparse,os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_npc_original_events import configuration as prior
def configuration(retained=False,isolate=False,shared=False):
 c=prior(retained,isolate,shared)
 c['stage']=ROOT/'.local/class-skills'/('npc-original-procs-shared'if shared else'npc-original-procs-isolate'if isolate else'npc-original-procs-retained'if retained else'npc-original-procs')
 if isolate:c['extra_families']=()
 if not shared:
  c['extra_families']+=('npc-original-procs',)
  c['extra_files']['npc-original-procs-cases.json']=ROOT/'.local/class-skills/npc-original-procs-source/cases.json'
 c['extra_inputs']+=(Path(__file__).resolve(),ROOT/'tools/probe_npc_original_procs.py')
 return c
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--retained',action='store_true');p.add_argument('--isolate',action='store_true');p.add_argument('--shared',action='store_true');a=p.parse_args()
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**configuration(a.retained,a.isolate,a.shared))
